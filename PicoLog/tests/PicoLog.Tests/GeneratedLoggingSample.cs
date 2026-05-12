namespace PicoLog.Tests;

public static partial class GeneratedLoggingSample
{
    public static void LogUserCreated(this ILogger logger, int id, string name)
    {
        logger.Log(LogLevel.Info, new EventId(1001, "UserCreated"), $"User {id} ({name}) created");
    }

    public static void LogDiskLow(this ILogger logger, int freeMb)
    {
        logger.Log(LogLevel.Warning, $"Disk space low: {freeMb} MB");
    }
}
