namespace PicoLog;

internal sealed class InternalLogger(
    string categoryName,
    LoggerFactoryRuntime runtime,
    CategoryPipeline pipeline
) : ILogger
{
    private readonly LoggerFactoryRuntime _runtime =
        runtime ?? throw new ArgumentNullException(nameof(runtime));
    private readonly CategoryPipeline _pipeline =
        pipeline ?? throw new ArgumentNullException(nameof(pipeline));

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return !_runtime.IsAcceptingWrites ? LoggerScopeProvider.Empty : _runtime.BeginScope(state);
    }

    public void Log(LogLevel logLevel, string message, Exception? exception = null) =>
        Write(logLevel, message, properties: null, exception);

    public void Log(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    ) => Write(logLevel, message, properties, exception);

    public Task LogAsync(
        LogLevel logLevel,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => WriteAsync(logLevel, message, properties: null, exception, cancellationToken);

    public Task LogAsync(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken = default
    ) => WriteAsync(logLevel, message, properties, exception, cancellationToken);

    public void Log(LogLevel logLevel, FormattableString message, Exception? exception = null) =>
        WriteFormatted(logLevel, message, properties: null, exception);

    public void Log(
        LogLevel logLevel,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    ) => WriteFormatted(logLevel, message, properties, exception);

    public Task LogAsync(
        LogLevel logLevel,
        FormattableString message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    ) => WriteFormattedAsync(logLevel, message, properties: null, exception, cancellationToken);

    public Task LogAsync(
        LogLevel logLevel,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken = default
    ) => WriteFormattedAsync(logLevel, message, properties, exception, cancellationToken);

    public void Log(LogLevel logLevel, EventId eventId, string message, Exception? exception = null)
    {
        if (!CanAcceptWrite(logLevel))
            return;

        var entry = CreateEntry(logLevel, message, exception, properties: null) with
        {
            EventId = eventId
        };
        _pipeline.Write(entry);
    }

    public void Log(
        LogLevel logLevel,
        EventId eventId,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    )
    {
        if (!CanAcceptWrite(logLevel))
            return;

        var entry = CreateEntry(logLevel, message, exception, properties) with
        {
            EventId = eventId
        };
        _pipeline.Write(entry);
    }

    public Task LogAsync(
        LogLevel logLevel,
        EventId eventId,
        string message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!CanAcceptWrite(logLevel))
            return Task.CompletedTask;

        var entry = CreateEntry(logLevel, message, exception, properties: null) with
        {
            EventId = eventId
        };

        return _pipeline.WriteAsync(entry, cancellationToken);
    }

    public Task LogAsync(
        LogLevel logLevel,
        EventId eventId,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken = default
    )
    {
        if (!CanAcceptWrite(logLevel))
            return Task.CompletedTask;

        var entry = CreateEntry(logLevel, message, exception, properties) with
        {
            EventId = eventId
        };

        return _pipeline.WriteAsync(entry, cancellationToken);
    }

    public void Log(
        LogLevel logLevel,
        EventId eventId,
        FormattableString message,
        Exception? exception = null
    )
    {
        if (!CanAcceptWrite(logLevel))
            return;

        var entry = CreateEntry(logLevel, message: null, exception, properties: null) with
        {
            MessageTemplate = message.Format,
            MessageArgs = message.GetArguments(),
            EventId = eventId
        };
        _pipeline.Write(entry);
    }

    public void Log(
        LogLevel logLevel,
        EventId eventId,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    )
    {
        if (!CanAcceptWrite(logLevel))
            return;

        var entry = CreateEntry(logLevel, message: null, exception, properties) with
        {
            MessageTemplate = message.Format,
            MessageArgs = message.GetArguments(),
            EventId = eventId
        };
        _pipeline.Write(entry);
    }

    public Task LogAsync(
        LogLevel logLevel,
        EventId eventId,
        FormattableString message,
        Exception? exception = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!CanAcceptWrite(logLevel))
            return Task.CompletedTask;

        var entry = CreateEntry(logLevel, message: null, exception, properties: null) with
        {
            MessageTemplate = message.Format,
            MessageArgs = message.GetArguments(),
            EventId = eventId
        };

        return _pipeline.WriteAsync(entry, cancellationToken);
    }

    public Task LogAsync(
        LogLevel logLevel,
        EventId eventId,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken = default
    )
    {
        if (!CanAcceptWrite(logLevel))
            return Task.CompletedTask;

        var entry = CreateEntry(logLevel, message: null, exception, properties) with
        {
            MessageTemplate = message.Format,
            MessageArgs = message.GetArguments(),
            EventId = eventId
        };

        return _pipeline.WriteAsync(entry, cancellationToken);
    }

    private void Write(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    )
    {
        if (!CanAcceptWrite(logLevel))
            return;

        var entry = CreateEntry(logLevel, message, exception, properties);
        _pipeline.Write(entry);
    }

    private Task WriteAsync(
        LogLevel logLevel,
        string message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken
    )
    {
        if (!CanAcceptWrite(logLevel))
            return Task.CompletedTask;

        var entry = CreateEntry(logLevel, message, exception, properties);

        return _pipeline.WriteAsync(entry, cancellationToken);
    }

    private void WriteFormatted(
        LogLevel logLevel,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception
    )
    {
        if (!CanAcceptWrite(logLevel))
            return;

        var entry = CreateEntry(logLevel, message: null, exception, properties) with
        {
            MessageTemplate = message.Format,
            MessageArgs = message.GetArguments()
        };
        _pipeline.Write(entry);
    }

    private Task WriteFormattedAsync(
        LogLevel logLevel,
        FormattableString message,
        IReadOnlyList<KeyValuePair<string, object?>>? properties,
        Exception? exception,
        CancellationToken cancellationToken
    )
    {
        if (!CanAcceptWrite(logLevel))
            return Task.CompletedTask;

        var entry = CreateEntry(logLevel, message: null, exception, properties) with
        {
            MessageTemplate = message.Format,
            MessageArgs = message.GetArguments()
        };

        return _pipeline.WriteAsync(entry, cancellationToken);
    }

    private bool CanAcceptWrite(LogLevel logLevel)
    {
        if (_runtime.IsAcceptingWrites)
            return _runtime.IsEnabled(logLevel, categoryName);
        _runtime.RecordRejectedAfterShutdown();
        return false;
    }

    private LogEntry CreateEntry(
        LogLevel logLevel,
        string? message,
        Exception? exception,
        IReadOnlyList<KeyValuePair<string, object?>>? properties
    )
    {
        var scopeSnapshot = _runtime.CaptureScopes();
        return new()
        {
            Timestamp = GetTimestamp(),
            Level = logLevel,
            Category = categoryName,
            Message = message,
            Exception = exception,
            Scopes = scopeSnapshot.Scopes,
            Properties = SnapshotProperties(properties),
            ScopeProperties = scopeSnapshot.Properties
        };
    }

    private static IReadOnlyList<KeyValuePair<string, object?>>? SnapshotProperties(
        IReadOnlyList<KeyValuePair<string, object?>>? properties
    )
    {
        if (properties is not { Count: > 0 })
            return null;

        return CopyProperties(properties);
    }

    private static KeyValuePair<string, object?>[] CopyProperties(
        IReadOnlyList<KeyValuePair<string, object?>> properties
    )
    {
        var snapshot = new KeyValuePair<string, object?>[properties.Count];

        for (var index = 0; index < properties.Count; index++)
            snapshot[index] = properties[index];

        return snapshot;
    }

    private DateTimeOffset GetTimestamp() => _runtime.TimestampProvider.GetLocalNow();
}
