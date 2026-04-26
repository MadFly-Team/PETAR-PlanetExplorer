using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace PEPAR.Modules.Debug
{
    internal enum DebugLogLevel
    {
        Trace,
        Debug,
        Information,
        Warning,
        Error,
        Critical
    }

    internal sealed record DebugLogEntry(
        DateTime Timestamp,
        DebugLogLevel Level,
        string Message,
        string? Category = null,
        string? ExceptionText = null)
    {
        public override string ToString()
        {
            var categoryText = string.IsNullOrWhiteSpace(Category) ? string.Empty : $" [{Category}]";
            var exceptionText = string.IsNullOrWhiteSpace(ExceptionText) ? string.Empty : $"{Environment.NewLine}{ExceptionText}";

            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level}] {Message}{categoryText}{exceptionText}";
        }
    }

    internal static class Debug_Logger
    {
        private const int MaxRetainedEntries = 5000;
        private static readonly object SyncRoot = new();
        private static readonly List<DebugLogEntry> Entries = [];

        private static string _logDirectory = Path.Combine(AppContext.BaseDirectory, "Logs");
        private static string _logFilePath = BuildLogFilePath(_logDirectory);
        private static Thread? _viewerThread;
        private static LogViewerForm? _viewerForm;

        internal static event Action<DebugLogEntry>? EntryLogged;

        internal static string LogFilePath
        {
            get
            {
                lock (SyncRoot)
                {
                    return _logFilePath;
                }
            }
        }

        internal static void Configure(string? logDirectory = null)
        {
            lock (SyncRoot)
            {
                if (!string.IsNullOrWhiteSpace(logDirectory))
                {
                    _logDirectory = logDirectory;
                }

                Directory.CreateDirectory(_logDirectory);
                _logFilePath = BuildLogFilePath(_logDirectory);
            }
        }

        internal static IReadOnlyList<DebugLogEntry> GetEntries()
        {
            lock (SyncRoot)
            {
                return Entries.ToList();
            }
        }

        internal static void LogTrace(string message, string? category = null) => Log(DebugLogLevel.Trace, message, category);

        internal static void LogDebug(string message, string? category = null) => Log(DebugLogLevel.Debug, message, category);

        internal static void LogInformation(string message, string? category = null) => Log(DebugLogLevel.Information, message, category);

        internal static void LogWarning(string message, string? category = null) => Log(DebugLogLevel.Warning, message, category);

        internal static void LogError(string message, Exception? exception = null, string? category = null) => Log(DebugLogLevel.Error, message, category, exception);

        internal static void LogCritical(string message, Exception? exception = null, string? category = null) => Log(DebugLogLevel.Critical, message, category, exception);

        internal static void Log(DebugLogLevel level, string message, string? category = null, Exception? exception = null)
        {
            var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "(empty message)" : message.Trim();
            var entry = new DebugLogEntry(DateTime.Now, level, normalizedMessage, category, exception?.ToString());

            lock (SyncRoot)
            {
                Directory.CreateDirectory(_logDirectory);
                _logFilePath = BuildLogFilePath(_logDirectory);
                File.AppendAllText(_logFilePath, entry + Environment.NewLine + Environment.NewLine);

                Entries.Add(entry);
                if (Entries.Count > MaxRetainedEntries)
                {
                    Entries.RemoveAt(0);
                }
            }

            System.Diagnostics.Debug.WriteLine(entry.ToString());
            EntryLogged?.Invoke(entry);
        }

        internal static void ShowLogViewer()
        {
            lock (SyncRoot)
            {
                if (_viewerForm is { IsDisposed: false })
                {
                    _viewerForm.BeginInvoke(new Action(() =>
                    {
                        _viewerForm.Show();
                        _viewerForm.BringToFront();
                        _viewerForm.Activate();
                    }));
                    return;
                }

                _viewerThread = new Thread(() =>
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    var form = new LogViewerForm(GetEntries());
                    lock (SyncRoot)
                    {
                        _viewerForm = form;
                    }

                    form.FormClosed += (_, _) =>
                    {
                        lock (SyncRoot)
                        {
                            _viewerForm = null;
                            _viewerThread = null;
                        }
                    };

                    Application.Run(form);
                })
                {
                    IsBackground = true,
                    Name = "PEPAR-LogViewer"
                };

                _viewerThread.SetApartmentState(ApartmentState.STA);
                _viewerThread.Start();
            }
        }

        internal static void CloseLogViewer()
        {
            lock (SyncRoot)
            {
                if (_viewerForm is null || _viewerForm.IsDisposed)
                {
                    return;
                }

                _viewerForm.BeginInvoke(new Action(() => _viewerForm.Close()));
            }
        }

        private static string BuildLogFilePath(string logDirectory)
        {
            return Path.Combine(logDirectory, $"PEPAR-{DateTime.Now:yyyyMMdd}.log");
        }
    }

    internal sealed class LogViewerForm : Form
    {
        private readonly ListBox _logList = new()
        {
            Dock = DockStyle.Fill,
            HorizontalScrollbar = true,
            IntegralHeight = false
        };

        internal LogViewerForm(IReadOnlyList<DebugLogEntry> existingEntries)
        {
            Text = "PEPAR Debug Log Viewer";
            Width = 1100;
            Height = 600;
            StartPosition = FormStartPosition.CenterScreen;

            var toolStrip = new ToolStrip();
            var copyButton = new ToolStripButton("Copy Selected");
            var openFolderButton = new ToolStripButton("Open Log Folder");
            var clearButton = new ToolStripButton("Clear View");

            copyButton.Click += (_, _) => CopySelectedEntries();
            openFolderButton.Click += (_, _) => OpenLogFolder();
            clearButton.Click += (_, _) => _logList.Items.Clear();

            toolStrip.Items.Add(copyButton);
            toolStrip.Items.Add(openFolderButton);
            toolStrip.Items.Add(clearButton);

            Controls.Add(_logList);
            Controls.Add(toolStrip);

            toolStrip.Dock = DockStyle.Top;

            foreach (var entry in existingEntries)
            {
                _logList.Items.Add(entry.ToString());
            }

            Debug_Logger.EntryLogged += HandleEntryLogged;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Debug_Logger.EntryLogged -= HandleEntryLogged;
            base.OnFormClosed(e);
        }

        private void HandleEntryLogged(DebugLogEntry entry)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action<DebugLogEntry>(HandleEntryLogged), entry);
                return;
            }

            _logList.Items.Add(entry.ToString());
            _logList.TopIndex = _logList.Items.Count - 1;
        }

        private void CopySelectedEntries()
        {
            if (_logList.SelectedItems.Count == 0)
            {
                return;
            }

            var selectedEntries = _logList.SelectedItems.Cast<object>()
                .Select(item => item?.ToString())
                .Where(text => !string.IsNullOrWhiteSpace(text));

            Clipboard.SetText(string.Join(Environment.NewLine, selectedEntries!));
        }

        private static void OpenLogFolder()
        {
            var folderPath = Path.GetDirectoryName(Debug_Logger.LogFilePath);
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = folderPath,
                UseShellExecute = true,
                Verb = "open"
            });
        }
    }
}
