namespace PicoLog.DI;

public static class SvcContainerExtensions
{
    extension(ISvcContainer container)
    {
        public ISvcContainer AddPicoLog(Action<LoggingOptions> configure) =>
            AddPicoLogCore(container, configure);

    }

#pragma warning disable IDE0051 // called by extension method AddPicoLog
    private static ISvcContainer AddPicoLogCore(
        ISvcContainer container,
        Action<LoggingOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new LoggingOptions();
        configure(options);
        LoggingOptions snapshot = options.CreateValidatedCopy();
        var sync = new Lock();
        ILoggerFactory? factory = null;

        ILoggerFactory ResolveFactory()
        {
            lock (sync)
                return factory ??= CreateLoggerFactory(container, snapshot);
        }

        var registrations = container
            .Register(new SvcDescriptor(typeof(ILoggerFactory), _ => ResolveFactory()))
            .RegisterSingleton(typeof(ILogger<>), typeof(Logger<>));

        return registrations;
    }
#pragma warning restore IDE0051

    private static ILoggerFactory CreateLoggerFactory(ISvcContainer container, LoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.ReadFrom.IncludeRegisteredSinks)
        {
            var sinks = CreateOwnedSinks(options);
            if (sinks.Count == 0)
                sinks.Add(new ColoredConsoleSink(options.Formatter));
            return new LoggerFactory(sinks, options.Factory);
        }

        var loggingScope = container.CreateScope();
        try
        {
            var sinks = ResolveRegisteredSinks(loggingScope);
            sinks.AddRange(CreateOwnedSinks(options));

            if (sinks.Count == 0)
                throw new InvalidOperationException(
                    "ReadFrom.RegisteredSinks requires at least one registered ILogSink when no explicit WriteTo sinks are configured."
                );

            return new OwnedLoggerFactory(new LoggerFactory(sinks, options.Factory), loggingScope);
        }
        catch
        {
            DisposeScopeSync(loggingScope);
            throw;
        }
    }

    private static IEnumerable<T> TryGetServices<T>(ISvcScope scope) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(scope);

        try
        {
            return scope.GetServices<T>();
        }
        catch (PicoDiException)
        {
            // PicoDI.Abs does not expose an IsRegistered<T>() query API.
            // Catching PicoDiException from GetServices<T>() is the only non-throwing
            // path to detect unregistered service types at this layer.
            return [];
        }
    }

    private static List<ILogSink> ResolveRegisteredSinks(ISvcScope scope)
    {
        var sinks = TryGetServices<ILogSink>(scope);
        return sinks.Select(NonOwningLogSink.Wrap).ToList();
    }

    private static List<ILogSink> CreateOwnedSinks(LoggingOptions options)
    {
        ILogFormatter formatter = options.Formatter;
        List<ILogSink> sinks = [];

        if (options.WriteTo.HasRegistrations)
        {
            foreach (var registration in options.WriteTo.Registrations)
            {
                switch (registration.Kind)
                {
                    case SinkConfiguration.SinkKind.Console:
                        sinks.Add(new ConsoleSink(formatter));
                        break;

                    case SinkConfiguration.SinkKind.ColoredConsole:
                        sinks.Add(new ColoredConsoleSink(formatter));
                        break;

                    case SinkConfiguration.SinkKind.File:
                        sinks.Add(new FileSink(formatter, registration.FileOptions!));
                        break;

                    case SinkConfiguration.SinkKind.Custom:
                        sinks.Add(registration.CreateSink!(formatter));
                        break;
                }
            }
        }

        return sinks;
    }

    private static void DisposeScopeSync(ISvcScope scope)
    {
        // ISvcScope is IAsyncDisposable only — no sync Dispose().
        // This path runs exclusively during error cleanup (factory creation
        // failure), when no processing threads exist yet, so pool starvation
        // cannot occur.
        Task.Run(async () => await scope.DisposeAsync().ConfigureAwait(false))
            .GetAwaiter()
            .GetResult();
    }
}
