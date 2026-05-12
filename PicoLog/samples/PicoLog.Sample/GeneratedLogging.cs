// Source-generated logging extension methods.
//
// These [PicoLogMessage] attribute declarations serve two purposes:
// 1. Document the intended source-generator extensions with their event metadata
// 2. Allow the PicoLog.Gen analyzer to produce AOT-compatible implementations
//    when the methods are declared as 'partial' without a body.
//
// Manual implementations are provided here as a fallback when the methods are
// not declared as 'partial', ensuring the sample builds and runs in all contexts.

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
