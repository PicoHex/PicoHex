// Set up the container and the default logging pipeline.

ISvcContainer container = new SvcContainer();

container
    .AddPicoLog(options =>
    {
        options.MinLevel = LogLevel.Debug;

        // Per-category filter: tune minimum level for a specific logger category prefix.
        // The last-matching rule wins. Here, Service category gets Debug (matches global).
        options.Factory.FilterRules.Add(
            new LogFilterRule("PicoLog.Sample.Service", LogLevel.Debug)
        );

        options.WriteTo.ColoredConsole();
        options.WriteTo.File("logs/app.log");
    })
    .ConfigureServices();

await using var scope = container.CreateScope();
var loggerFactory = scope.GetService<ILoggerFactory>();

// Run the sample workload.
var service = scope.GetService<IService>();

await service.WriteLogAsync();

await loggerFactory.DisposeAsync();

// The explicit disposal flushes queued log entries before exit.
