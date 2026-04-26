using System;
using PEPAR.Modules.Debug;

Debug_Logger.Configure();
Debug_Logger.LogInformation("Starting PEPAR.", "Startup");
//Debug_Logger.ShowLogViewer();

try
{
    using var game = new PEPAR.PEPAR_Ship();
    Debug_Logger.LogInformation("Game instance created.", "Startup");
    game.Run();
    Debug_Logger.LogInformation("Game loop exited normally.", "Shutdown");
}
catch (Exception ex)
{
    Debug_Logger.LogCritical("Unhandled exception during application execution.", ex, "Startup");
    throw;
}
finally
{
    //Debug_Logger.LogInformation("Closing debug log viewer.", "Shutdown");
    //Debug_Logger.CloseLogViewer();
}
