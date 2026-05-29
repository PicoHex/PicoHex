
Console.WriteLine("=== PicoCfg.DI Sample ===");

await using var root = await Cfg.CreateBuilder()
    .Add(
        new Dictionary<string, string>
        {
            ["App:Name"] = "PicoCfg.DI",
            ["App:Count"] = "42",
            ["Request:Name"] = "Scoped Request",
            ["Request:Count"] = "7",
        }
    )
    .BuildAsync();

await using var container = new SvcContainer(autoConfigureFromGenerator: false);

container
    .RegisterCfgRoot(root)
    .RegisterCfgSingleton<AppSettings>("App")
    .RegisterCfgScoped<RequestSettings>("Request")
    .RegisterCfgOptionsSingleton<AppSettings>("App");

await using var scope = container.CreateScope();
var cfg = scope.GetService<ICfg>();
var resolvedRoot = scope.GetService<ICfgRoot>();
var app = scope.GetService<AppSettings>();
var request1 = scope.GetService<RequestSettings>();
var request2 = scope.GetService<RequestSettings>();

Console.WriteLine($"Cfg App:Name = {cfg.GetValue("App:Name")}");
Console.WriteLine($"Root App:Name = {resolvedRoot.GetValue("App:Name")}");
Console.WriteLine($"Singleton Name = {app.Name}, Count = {app.Count}");
Console.WriteLine($"Scoped Name = {request1.Name}, Count = {request1.Count}");
Console.WriteLine($"Scoped Same Instance = {ReferenceEquals(request1, request2)}");

AssertEqual("Cfg App:Name", cfg.GetValue("App:Name"), "PicoCfg.DI");
AssertEqual("Root App:Name", resolvedRoot.GetValue("App:Name"), "PicoCfg.DI");
AssertEqual("Singleton Name", app.Name, "PicoCfg.DI");
AssertEqual("Singleton Count", app.Count, 42);
AssertEqual("Scoped Name", request1.Name, "Scoped Request");
AssertEqual("Scoped Count", request1.Count, 7);
AssertTrue("Scoped Same Instance", ReferenceEquals(request1, request2));

Console.WriteLine("=== Options Pattern via DI ===");

// Register and resolve ICfgOptions<AppSettings>
var options = scope.GetService<ICfgOptions<AppSettings>>();
Console.WriteLine($"  Options Name = {options!.Value.Name}");
Console.WriteLine($"  Options Count = {options.Value.Count}");

static void AssertEqual<T>(string name, T actual, T expected)
{
    if (!EqualityComparer<T>.Default.Equals(actual, expected))
    {
        throw new InvalidOperationException(
            $"{name} mismatch. Expected '{expected}' but got '{actual}'."
        );
    }
}

static void AssertTrue(string name, bool condition)
{
    if (!condition)
    {
        throw new InvalidOperationException($"{name} expected true.");
    }
}

public sealed class AppSettings
{
    public string? Name { get; set; }
    public int Count { get; set; }
}

public sealed class RequestSettings
{
    public string? Name { get; set; }
    public int Count { get; set; }
}
