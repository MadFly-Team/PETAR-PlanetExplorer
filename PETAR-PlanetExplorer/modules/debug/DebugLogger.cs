using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PETAR_PlanetExplorer.Modules.Debug
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    public static class DebugLogger
    {
        private static readonly object SyncRoot = new();
        private static StreamWriter _writer;
        private static bool _isInitialized;

        public static string LogFilePath { get; private set; } = string.Empty;

        public static LogLevel MinimumLevel { get; set; } = LogLevel.Debug;

        public static void Initialize(string applicationName)
        {
            lock (SyncRoot)
            {
                if (_isInitialized)
                {
                    return;
                }

                var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logDirectory);

                var safeApplicationName = MakeSafeFileName(applicationName);
                LogFilePath = Path.Combine(logDirectory, $"{safeApplicationName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
                _writer = new StreamWriter(new FileStream(LogFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    AutoFlush = true
                };

                _isInitialized = true;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            }

            Info($"Debug logging initialized. Writing to '{LogFilePath}'.");
        }

        public static void Debug(string message)
        {
            Write(LogLevel.Debug, message);
        }

        public static void Info(string message)
        {
            Write(LogLevel.Info, message);
        }

        public static void Warning(string message)
        {
            Write(LogLevel.Warning, message);
        }

        public static void Error(string message, Exception exception = null)
        {
            Write(LogLevel.Error, message, exception);
        }

        public static void Critical(string message, Exception exception = null)
        {
            Write(LogLevel.Critical, message, exception);
        }

        public static void Shutdown()
        {
            lock (SyncRoot)
            {
                if (!_isInitialized)
                {
                    return;
                }

                WriteCore(LogLevel.Info, "Shutting down debug logging.", null);

                AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

                _writer?.Dispose();
                _writer = null;
                _isInitialized = false;
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            if (args.ExceptionObject is Exception exception)
            {
                Critical("Unhandled application exception.", exception);
                return;
            }

            Critical($"Unhandled application exception: {args.ExceptionObject}");
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args)
        {
            Critical("Unobserved task exception.", args.Exception);
        }

        private static void Write(LogLevel level, string message, Exception exception = null)
        {
            if (level < MinimumLevel)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (!_isInitialized || _writer == null)
                {
                    return;
                }

                WriteCore(level, message, exception);
            }
        }

        private static void WriteCore(LogLevel level, string message, Exception exception)
        {
            var entry = new StringBuilder()
                .Append('[')
                .Append(DateTime.UtcNow.ToString("O"))
                .Append("] [")
                .Append(level)
                .Append("] [Thread ")
                .Append(Environment.CurrentManagedThreadId)
                .Append("] ")
                .Append(message);

            if (exception != null)
            {
                entry.AppendLine();
                entry.Append(exception);
            }

            _writer.WriteLine(entry.ToString());
        }

        private static string MakeSafeFileName(string value)
        {
            var invalidFileNameChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);

            foreach (var character in value)
            {
                builder.Append(Array.IndexOf(invalidFileNameChars, character) >= 0 ? '_' : character);
            }

            return builder.ToString();
        }
    }
}
