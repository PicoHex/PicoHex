namespace PicoLog.Abs;

/// <summary>
/// Provides extension methods for <see cref="ILogger"/> to enable convenient
/// level-specific logging (Trace, Debug, Info, etc.) with support for string messages,
/// <see cref="FormattableString"/> templates, <see cref="EventId"/> tags,
/// and asynchronous variants.
/// </summary>
public static partial class LoggerExtensions
{
    // ── Convenience overloads (default parameters) moved from ILogger interface ──

    extension(ILogger logger)
    {
        // string

        public void Log(LogLevel logLevel, string message, Exception? exception = null) =>
            logger.Log(logLevel, message, null, exception);

        public void Log(LogLevel logLevel, string message,
            IReadOnlyList<KeyValuePair<string, object?>>? properties,
            Exception? exception = null) =>
            logger.Log(logLevel, message, properties, exception);

        public Task LogAsync(LogLevel logLevel, string message,
            Exception? exception = null, CancellationToken cancellationToken = default) =>
            logger.LogAsync(logLevel, message, null, exception, cancellationToken);

        public Task LogAsync(LogLevel logLevel, string message,
            IReadOnlyList<KeyValuePair<string, object?>>? properties,
            Exception? exception = null, CancellationToken cancellationToken = default) =>
            logger.LogAsync(logLevel, message, properties, exception, cancellationToken);

        // EventId + string

        public void Log(LogLevel logLevel, EventId eventId, string message, Exception? exception = null) =>
            logger.Log(logLevel, eventId, message, null, exception);

        public void Log(LogLevel logLevel, EventId eventId, string message,
            IReadOnlyList<KeyValuePair<string, object?>>? properties,
            Exception? exception = null) =>
            logger.Log(logLevel, eventId, message, properties, exception);

        public Task LogAsync(LogLevel logLevel, EventId eventId, string message,
            Exception? exception = null, CancellationToken cancellationToken = default) =>
            logger.LogAsync(logLevel, eventId, message, null, exception, cancellationToken);

        public Task LogAsync(LogLevel logLevel, EventId eventId, string message,
            IReadOnlyList<KeyValuePair<string, object?>>? properties,
            Exception? exception = null, CancellationToken cancellationToken = default) =>
            logger.LogAsync(logLevel, eventId, message, properties, exception, cancellationToken);

        // FormattableString

        public void Log(LogLevel logLevel, FormattableString message, Exception? exception = null) =>
            logger.Log(logLevel, message, null, exception);

        public void Log(LogLevel logLevel, FormattableString message,
            IReadOnlyList<KeyValuePair<string, object?>>? properties,
            Exception? exception = null) =>
            logger.Log(logLevel, message, properties, exception);

        public Task LogAsync(LogLevel logLevel, FormattableString message,
            Exception? exception = null, CancellationToken cancellationToken = default) =>
            logger.LogAsync(logLevel, message, null, exception, cancellationToken);

        public Task LogAsync(LogLevel logLevel, FormattableString message,
            IReadOnlyList<KeyValuePair<string, object?>>? properties,
            Exception? exception = null, CancellationToken cancellationToken = default) =>
            logger.LogAsync(logLevel, message, properties, exception, cancellationToken);

        // EventId + FormattableString

        public void Log(LogLevel logLevel, EventId eventId, FormattableString message,
            Exception? exception = null) =>
            logger.Log(logLevel, eventId, message, null, exception);

        public void Log(LogLevel logLevel, EventId eventId, FormattableString message,
            IReadOnlyList<KeyValuePair<string, object?>>? properties,
            Exception? exception = null) =>
            logger.Log(logLevel, eventId, message, properties, exception);

        public Task LogAsync(LogLevel logLevel, EventId eventId, FormattableString message,
            Exception? exception = null, CancellationToken cancellationToken = default) =>
            logger.LogAsync(logLevel, eventId, message, null, exception, cancellationToken);

        public Task LogAsync(LogLevel logLevel, EventId eventId, FormattableString message,
            IReadOnlyList<KeyValuePair<string, object?>>? properties,
            Exception? exception = null, CancellationToken cancellationToken = default) =>
            logger.LogAsync(logLevel, eventId, message, properties, exception, cancellationToken);
    }

    extension(ILogger logger)
    {
        /// <summary>
        /// Convenience helper for structured logging on <see cref="ILogger"/>.
        /// </summary>
        /// <remarks>
        /// This helper forwards to the native <see cref="ILogger.Log(LogLevel, string, IReadOnlyList{KeyValuePair{string, object?}}?, Exception?)"/>
        /// overload.
        /// </remarks>
        public void LogStructured(
            LogLevel logLevel,
            string message,
            IReadOnlyList<KeyValuePair<string, object?>>? properties = null,
            Exception? exception = null
        ) => logger.Log(logLevel, message, properties, exception);

        /// <summary>
        /// Convenience helper for structured logging on <see cref="ILogger"/>.
        /// </summary>
        /// <remarks>
        /// This helper forwards to the native <see cref="ILogger.LogAsync(LogLevel, string, IReadOnlyList{KeyValuePair{string, object?}}?, Exception?, CancellationToken)"/>
        /// overload.
        /// </remarks>
        public Task LogStructuredAsync(
            LogLevel logLevel,
            string message,
            IReadOnlyList<KeyValuePair<string, object?>>? properties = null,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(logLevel, message, properties, exception, cancellationToken);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Trace"/> level.
        /// </summary>
        /// <remarks>
        /// Use for detailed debugging information that's typically only relevant during development.
        /// </remarks>
        public void Trace(string message, Exception? exception = null) =>
            logger.Log(LogLevel.Trace, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Debug"/> level.
        /// </summary>
        /// <remarks>
        /// Use for debugging information that's useful in production troubleshooting.
        /// </remarks>
        public void Debug(string message, Exception? exception = null) =>
            logger.Log(LogLevel.Debug, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Info"/> level.
        /// </summary>
        /// <remarks>
        /// Use for general application flow tracking and significant events.
        /// </remarks>
        public void Info(string message, Exception? exception = null) =>
            logger.Log(LogLevel.Info, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Notice"/> level.
        /// </summary>
        /// <remarks>
        /// Use for important runtime events that require attention but aren't errors.
        /// </remarks>
        public void Notice(string message, Exception? exception = null) =>
            logger.Log(LogLevel.Notice, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Warning"/> level.
        /// </summary>
        /// <remarks>
        /// Use for unexpected or problematic situations that aren't immediately harmful.
        /// </remarks>
        public void Warning(string message, Exception? exception = null) =>
            logger.Log(LogLevel.Warning, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Error"/> level.
        /// </summary>
        /// <remarks>
        /// Use for errors that impact specific operations but allow the application to continue.
        /// </remarks>
        public void Error(string message, Exception? exception = null) =>
            logger.Log(LogLevel.Error, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Critical"/> level.
        /// </summary>
        /// <remarks>
        /// Use for severe failures that require immediate attention and may crash the application.
        /// </remarks>
        public void Critical(string message, Exception? exception = null) =>
            logger.Log(LogLevel.Critical, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Alert"/> level.
        /// </summary>
        /// <remarks>
        /// Use when immediate action is required (e.g., loss of primary database connection).
        /// </remarks>
        public void Alert(string message, Exception? exception = null) =>
            logger.Log(LogLevel.Alert, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Emergency"/> level.
        /// </summary>
        /// <remarks>
        /// Reserve for catastrophic system-wide failures where the system is unusable.
        /// </remarks>
        public void Emergency(string message, Exception? exception = null) =>
            logger.Log(LogLevel.Emergency, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Trace"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use for detailed debugging information that's typically only relevant during development.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public void Trace(FormattableString message, Exception? exception = null) =>
            logger.Log(LogLevel.Trace, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Debug"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use for debugging information that's useful in production troubleshooting.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public void Debug(FormattableString message, Exception? exception = null) =>
            logger.Log(LogLevel.Debug, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Info"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use for general application flow tracking and significant events.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public void Info(FormattableString message, Exception? exception = null) =>
            logger.Log(LogLevel.Info, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Notice"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use for important runtime events that require attention but aren't errors.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public void Notice(FormattableString message, Exception? exception = null) =>
            logger.Log(LogLevel.Notice, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Warning"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use for unexpected or problematic situations that aren't immediately harmful.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public void Warning(FormattableString message, Exception? exception = null) =>
            logger.Log(LogLevel.Warning, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Error"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use for errors that impact specific operations but allow the application to continue.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public void Error(FormattableString message, Exception? exception = null) =>
            logger.Log(LogLevel.Error, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Critical"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use for severe failures that require immediate attention and may crash the application.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public void Critical(FormattableString message, Exception? exception = null) =>
            logger.Log(LogLevel.Critical, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Alert"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use when immediate action is required (e.g., loss of primary database connection).
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public void Alert(FormattableString message, Exception? exception = null) =>
            logger.Log(LogLevel.Alert, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Emergency"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Reserve for catastrophic system-wide failures where the system is unusable.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public void Emergency(FormattableString message, Exception? exception = null) =>
            logger.Log(LogLevel.Emergency, message, exception);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Trace"/> level.
        /// </summary>
        /// <remarks>
        /// Prefer this async version for I/O-bound operations in async contexts.
        /// </remarks>
        public Task TraceAsync(string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Trace, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Debug"/> level.
        /// </summary>
        /// <remarks>
        /// Use for debugging information that's useful in production troubleshooting.
        /// </remarks>
        public Task DebugAsync(string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Debug, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Info"/> level.
        /// </summary>
        /// <remarks>
        /// Use for general application flow tracking and significant events.
        /// </remarks>
        public Task InfoAsync(string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Info, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Notice"/> level.
        /// </summary>
        /// <remarks>
        /// Use for important runtime events that require attention but aren't errors.
        /// </remarks>
        public Task NoticeAsync(string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Notice, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Warning"/> level.
        /// </summary>
        /// <remarks>
        /// Use for unexpected or problematic situations that aren't immediately harmful.
        /// </remarks>
        public Task WarningAsync(string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Warning, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Error"/> level.
        /// </summary>
        /// <remarks>
        /// Use for errors that impact specific operations but allow the application to continue.
        /// </remarks>
        public Task ErrorAsync(string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Error, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Critical"/> level.
        /// </summary>
        /// <remarks>
        /// Use for severe failures that require immediate attention and may crash the application.
        /// </remarks>
        public Task CriticalAsync(string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Critical, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Alert"/> level.
        /// </summary>
        /// <remarks>
        /// Use when immediate action is required (e.g., loss of primary database connection).
        /// </remarks>
        public Task AlertAsync(string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Alert, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Emergency"/> level.
        /// </summary>
        /// <remarks>
        /// Reserve for catastrophic system-wide failures where the system is unusable.
        /// </remarks>
        public Task EmergencyAsync(string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Emergency, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Trace"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Prefer this async version for I/O-bound operations in async contexts.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public Task TraceAsync(FormattableString message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Trace, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Debug"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use for debugging information that's useful in production troubleshooting.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public Task DebugAsync(FormattableString message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Debug, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Info"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use for general application flow tracking and significant events.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public Task InfoAsync(FormattableString message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Info, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Notice"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use for important runtime events that require attention but aren't errors.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public Task NoticeAsync(FormattableString message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Notice, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Warning"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use for unexpected or problematic situations that aren't immediately harmful.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public Task WarningAsync(FormattableString message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Warning, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Error"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use for errors that impact specific operations but allow the application to continue.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public Task ErrorAsync(FormattableString message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Error, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Critical"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use for severe failures that require immediate attention and may crash the application.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public Task CriticalAsync(FormattableString message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Critical, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Alert"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Use when immediate action is required (e.g., loss of primary database connection).
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public Task AlertAsync(FormattableString message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Alert, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Emergency"/> level using a <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// Reserve for catastrophic system-wide failures where the system is unusable.
        /// When called with an interpolated string, the template and arguments are preserved for deferred formatting.
        /// </remarks>
        public Task EmergencyAsync(FormattableString message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Emergency, message, exception, cancellationToken);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Trace"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use for detailed debugging information that's typically only relevant during development.
        /// </remarks>
        public void Trace(EventId eventId, string message, Exception? exception = null) =>
            logger.Log(LogLevel.Trace, eventId, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Debug"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use for debugging information that's useful in production troubleshooting.
        /// </remarks>
        public void Debug(EventId eventId, string message, Exception? exception = null) =>
            logger.Log(LogLevel.Debug, eventId, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Info"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use for general application flow tracking and significant events.
        /// </remarks>
        public void Info(EventId eventId, string message, Exception? exception = null) =>
            logger.Log(LogLevel.Info, eventId, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Notice"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use for important runtime events that require attention but aren't errors.
        /// </remarks>
        public void Notice(EventId eventId, string message, Exception? exception = null) =>
            logger.Log(LogLevel.Notice, eventId, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Warning"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use for unexpected or problematic situations that aren't immediately harmful.
        /// </remarks>
        public void Warning(EventId eventId, string message, Exception? exception = null) =>
            logger.Log(LogLevel.Warning, eventId, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Error"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use for errors that impact specific operations but allow the application to continue.
        /// </remarks>
        public void Error(EventId eventId, string message, Exception? exception = null) =>
            logger.Log(LogLevel.Error, eventId, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Critical"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use for severe failures that require immediate attention and may crash the application.
        /// </remarks>
        public void Critical(EventId eventId, string message, Exception? exception = null) =>
            logger.Log(LogLevel.Critical, eventId, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Alert"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use when immediate action is required (e.g., loss of primary database connection).
        /// </remarks>
        public void Alert(EventId eventId, string message, Exception? exception = null) =>
            logger.Log(LogLevel.Alert, eventId, message, exception);

        /// <summary>
        /// Logs a message at <see cref="LogLevel.Emergency"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Reserve for catastrophic system-wide failures where the system is unusable.
        /// </remarks>
        public void Emergency(EventId eventId, string message, Exception? exception = null) =>
            logger.Log(LogLevel.Emergency, eventId, message, exception);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Trace"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Prefer this async version for I/O-bound operations in async contexts.
        /// </remarks>
        public Task TraceAsync(EventId eventId, string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Trace, eventId, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Debug"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use for debugging information that's useful in production troubleshooting.
        /// </remarks>
        public Task DebugAsync(EventId eventId, string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Debug, eventId, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Info"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use for general application flow tracking and significant events.
        /// </remarks>
        public Task InfoAsync(EventId eventId, string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Info, eventId, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Notice"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use for important runtime events that require attention but aren't errors.
        /// </remarks>
        public Task NoticeAsync(EventId eventId, string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Notice, eventId, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Warning"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use for unexpected or problematic situations that aren't immediately harmful.
        /// </remarks>
        public Task WarningAsync(EventId eventId, string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Warning, eventId, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Error"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use for errors that impact specific operations but allow the application to continue.
        /// </remarks>
        public Task ErrorAsync(EventId eventId, string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Error, eventId, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Critical"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use for severe failures that require immediate attention and may crash the application.
        /// </remarks>
        public Task CriticalAsync(EventId eventId, string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Critical, eventId, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Alert"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Use when immediate action is required (e.g., loss of primary database connection).
        /// </remarks>
        public Task AlertAsync(EventId eventId, string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Alert, eventId, message, exception, cancellationToken);

        /// <summary>
        /// Asynchronously logs a message at <see cref="LogLevel.Emergency"/> level with an <see cref="EventId"/>.
        /// </summary>
        /// <remarks>
        /// Reserve for catastrophic system-wide failures where the system is unusable.
        /// </remarks>
        public Task EmergencyAsync(EventId eventId, string message,
            Exception? exception = null,
            CancellationToken cancellationToken = default
        ) => logger.LogAsync(LogLevel.Emergency, eventId, message, exception, cancellationToken);
    }
}
