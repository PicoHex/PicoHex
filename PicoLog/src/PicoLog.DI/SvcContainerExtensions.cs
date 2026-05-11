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
            {
                loggingScope.DisposeAsync().GetAwaiter().GetResult();
                throw new InvalidOperationException(
                    "ReadFrom.RegisteredSinks requires at least one registered ILogSink when no explicit WriteTo sinks are configured."
                );
            }

            return new OwnedLoggerFactory(new LoggerFactory(sinks, options.Factory), loggingScope);
        }
        catch
        {
            loggingScope.DisposeAsync().GetAwaiter().GetResult();
            throw;
        }
    }

    private static List<ILogSink> ResolveRegisteredSinks(ISvcScope scope)
    {
        try
        {
            return scope.GetServices<ILogSink>().Select(NonOwningLogSink.Wrap).ToList();
        }
        catch (PicoDiException ex) when (IsNotRegistered(ex))
        {
            return [];
        }
    }

    // FRAGILE: depends on PicoDiException.Message text matching.
    // Replace when PicoDI provides TryGetServices<T>() or a dedicated
    // ServiceNotRegisteredException subtype.
    private static bool IsNotRegistered(PicoDiException ex) =>
        ex.Message.Contains("is not registered", StringComparison.Ordinal);

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
}
