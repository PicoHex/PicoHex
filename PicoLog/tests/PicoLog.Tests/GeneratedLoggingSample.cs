namespace PicoLog.Tests;

internal static class GeneratedLoggingSample
{
    [PicoLogMessage(
        LogLevel.Info,
        EventId = 1001,
        EventName = "UserCreated",
        Message = "User {id} ({name}) created"
    )]
    internal static void LogUserCreated(this ILogger logger, int id, string name)
    {
        logger.Log(LogLevel.Info, new EventId(1001, "UserCreated"), $"User {id} ({name}) created");
    }

    [PicoLogMessage(LogLevel.Warning, Message = "Disk space low: {freeMb} MB")]
    internal static void LogDiskLow(this ILogger logger, int freeMb)
    {
        logger.Log(LogLevel.Warning, $"Disk space low: {freeMb} MB");
    }
}
