// ── Demo: PicoDI SvcHost — DI Host with Hosted Services ──────────────

Console.WriteLine("=== PicoDI SvcHost Demo ===\n");

// 1. SvcHostBuilder: configure services and hosted services
// 2. BuildAsync: instantiate container, build, and start all hosted services
// 3. IHostedLifecycleSvc: fine-grained start/stop phases
// 4. BackgroundSvc: long-running background work
// 5. Constructor injection into hosted services

var builder = new SvcHostBuilder();

builder.ConfigureServices(container =>
{
    // ── Shared services (used by hosted services) ──────────────────
    container.RegisterSingleton<IClock, SystemClock>();
    container.RegisterSingleton<IGreeter, Greeter>();

    // ── Hosted services ────────────────────────────────────────────
    container.RegisterHostedSvc<StartupHostedSvc>(sp => new StartupHostedSvc(
        sp.GetService<IGreeter>()!
    ));
    container.RegisterHostedSvc<PeriodicBackgroundSvc>(sp => new PeriodicBackgroundSvc(
        sp.GetService<IGreeter>()!
    ));
    container.RegisterHostedSvc<TimedLifecycleSvc>(sp => new TimedLifecycleSvc(
        sp.GetService<IClock>()!
    ));
});

// ── Build + Start (hosted services start automatically) ──────────────

await using var host = await builder.BuildAsync();

Console.WriteLine("\n=== Host is running (press Enter to stop) ===\n");
Console.ReadLine();

// ── Explicitly dispose to stop hosted services before exit ───────────
Console.WriteLine("\n=== Shutting down... ===\n");
await host.DisposeAsync();

Console.WriteLine("\n=== PicoDI SvcHost demo complete ===");
