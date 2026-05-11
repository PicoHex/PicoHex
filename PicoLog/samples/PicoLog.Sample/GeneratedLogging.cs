// Source-generated logging extension methods.
//
// When the PicoLog.Generators analyzer is active, these methods are emitted
// automatically from [PicoLogMessage] partial method declarations.
// Manual implementations are provided here as a fallback — the [PicoLogMessage]
// attributes document the intended source-generator declarations.

namespace PicoLog.Sample;

public static partial class GeneratedLogging
{
    [PicoLogMessage(
        LogLevel.Info,
        EventId = 1001,
        EventName = "UserLogin",
        Message = "User {userName} ({userId}) logged in from {ip}"
    )]
    public static void LogUserLogin(this ILogger logger, string userName, int userId, string ip) =>
        logger.Log(
            LogLevel.Info,
            new EventId(1001, "UserLogin"),
            $"User {userName} ({userId}) logged in from {ip}"
        );

    [PicoLogMessage(
        LogLevel.Warning,
        EventId = 5001,
        EventName = "DiskLow",
        Message = "Disk space is low: {freeMb:N0} MB remaining on {drive}"
    )]
    public static void LogDiskLow(this ILogger logger, long freeMb, string drive) =>
        logger.Log(
            LogLevel.Warning,
            new EventId(5001, "DiskLow"),
            $"Disk space is low: {freeMb:N0} MB remaining on {drive}"
        );
}
