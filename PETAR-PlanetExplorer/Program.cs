using System;
using PETAR_PlanetExplorer.Modules.Debug;

DebugLogger.Initialize("PETAR-PlanetExplorer");

try
{
    DebugLogger.Info("Starting game runtime.");

    using var game = new PETAR_PlanetExplorer.PETARPlanetExplorer();
    game.Run();

    DebugLogger.Info("Game runtime stopped normally.");
}
catch (Exception exception)
{
    DebugLogger.Critical("Unhandled exception while running the game.", exception);
    throw;
}
finally
{
    DebugLogger.Shutdown();
}
