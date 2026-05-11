using PicoCfg;
using PicoCfg.Extensions;

Console.WriteLine("=== PicoCfg.Gen AOT Binding Sample ===");

await using var root = await Cfg
    .CreateBuilder()
    .Add(new Dictionary<string, string>
    {
        ["App:Name"] = "PicoCfg.Gen",
        ["App:Enabled"] = "true",
        ["App:Count"] = "42",
        ["App:Mode"] = "Advanced",
    })
    .BuildAsync();

var settings = CfgBind.Bind<AppSettings>(root, "App");

Console.WriteLine($"Name: {settings.Name}");
Console.WriteLine($"Enabled: {settings.Enabled}");
Console.WriteLine($"Count: {settings.Count}");
Console.WriteLine($"Mode: {settings.Mode}");

AssertEqual("Bind Name", settings.Name, "PicoCfg.Gen");
AssertEqual("Bind Enabled", settings.Enabled, true);
AssertEqual("Bind Count", settings.Count, 42);
AssertEqual("Bind Mode", settings.Mode, SampleMode.Advanced);

var existing = new AppSettings
{
    Name = "Before",
    Enabled = false,
    Count = 0,
    Mode = SampleMode.Basic,
};

CfgBind.BindInto(root, existing, "App");

Console.WriteLine();
Console.WriteLine("BindInto result:");
Console.WriteLine($"Name: {existing.Name}");
Console.WriteLine($"Enabled: {existing.Enabled}");
Console.WriteLine($"Count: {existing.Count}");
Console.WriteLine($"Mode: {existing.Mode}");

AssertEqual("BindInto Name", existing.Name, "PicoCfg.Gen");
AssertEqual("BindInto Enabled", existing.Enabled, true);
AssertEqual("BindInto Count", existing.Count, 42);
AssertEqual("BindInto Mode", existing.Mode, SampleMode.Advanced);

return 0;

static void AssertEqual<T>(string name, T actual, T expected)
{
    if (!EqualityComparer<T>.Default.Equals(actual, expected))
    {
        throw new InvalidOperationException($"{name} mismatch. Expected '{expected}' but got '{actual}'.");
    }
}

public sealed class AppSettings
{
    public string? Name { get; set; }
    public bool Enabled { get; set; }
    public int Count { get; set; }
    public SampleMode Mode { get; set; }
}

public enum SampleMode
{
    Basic,
    Advanced,
}
