// ── Demo: all PicoDI registration methods ──────────────────────────────

Console.WriteLine("=== PicoDI Registration Methods Demo ===\n");

// 1. Open generic registration (runtime, for IRepository<T> → InMemoryRepository<T>)
// 2. Type-based marker registration (compile-time, source-generator picks up)
// 3. Factory-based registration (runtime, with lambda)
// 4. Instance registration (runtime, pre-existing singleton)
// 5. Batch registration (runtime, RegisterRange)

await using SvcContainer container = new();

// ── 1. Open generic ──────────────────────────────────────────────
container.RegisterTransient(typeof(IRepository<>), typeof(InMemoryRepository<>));

// ── 2. Type-based markers (source generator emits factory code) ──
container.RegisterSingleton<IClock, SystemClock>();
container.RegisterTransient<IGreeter, Greeter>();
container.RegisterTransient<IWelcomeService, WelcomeService>();

// ── 3. Factory-based (runtime) ────────────────────────────────────
container.RegisterTransient<INotifier>(static _ => new EmailNotifier());
container.RegisterSingleton<INotifier>(static _ => new SmsNotifier());

// ── 4. Instance registration ──────────────────────────────────────
container.RegisterSingle<IClock>(new SystemClock());

// ── 5. Scoped service ────────────────────────────────────────────
container.RegisterScoped<IGreeter, Greeter>();

// ── Build (optional: CreateScope() auto-builds on first use) ───────────
Console.WriteLine("Container ready.\n");

// ── Resolve and demonstrate ────────────────────────────────────────────
await using var scope = container.CreateScope();

// IGreeter (Scoped — wins over Transient since it's last)
var greeter = scope.GetService<IGreeter>();
Console.WriteLine($"[IGreeter]        {greeter.Greet("World")}");

// WelcomeService with [SvcConstructor] (Transient, preferred ctor used)
var welcome = scope.GetService<IWelcomeService>();
Console.WriteLine($"[IWelcomeService] {welcome.GetWelcomeMessage()}");

// Multiple INotifier implementations (GetServices)
Console.WriteLine("[INotifier]       All channels:");
foreach (var n in scope.GetServices<INotifier>())
    n.Notify("Hello from PicoDI!");

// Open generic IRepository<T> (Transient)
var userRepo = scope.GetService<IRepository<string>>();
userRepo.Add("Alice");
userRepo.Add("Bob");
Console.WriteLine($"[IRepository<T>]  Users: {string.Join(", ", userRepo.GetAll())}");

Console.WriteLine("\n=== All registrations demonstrated successfully ===");
