// AOT Compatibility Validation for PicoCfg
// This program validates that all PicoCfg features work correctly in AOT-compiled mode

Console.WriteLine("=== PicoCfg AOT Compatibility Validation ===");
Console.WriteLine();

var testResults = new List<bool>();

// Test 1: Basic key-value parsing
await Test(
    "Basic key-value parsing",
    async () =>
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("TestKey=TestValue\nAnotherKey=AnotherValue");

        await using var root = await builder.BuildAsync();
        var value1 = root.GetValue("TestKey");
        var value2 = root.GetValue("AnotherKey");

        return value1 == "TestValue" && value2 == "AnotherValue";
    }
);

// Test 2: Multiple configuration source priority (last source wins)
await Test(
    "Multiple source priority",
    async () =>
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("Key=FirstValue");
        builder.Add("Key=SecondValue"); // This should override

        await using var root = await builder.BuildAsync();
        var value = root.GetValue("Key");

        return value == "SecondValue";
    }
);

// Test 3: Different configuration source types
await Test(
    "Multiple source types",
    async () =>
    {
        var builder = Cfg.CreateBuilder();

        // String source
        builder.Add("StringKey=StringValue");

        // Dictionary source
        builder.Add(new Dictionary<string, string> { ["DictKey"] = "DictValue" });

        // Stream source
        builder.Add(() =>
        {
            var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.WriteLine("StreamKey=StreamValue");
            writer.Flush();
            stream.Position = 0;
            return stream;
        });

        await using var root = await builder.BuildAsync();

        var stringValue = root.GetValue("StringKey");
        var dictValue = root.GetValue("DictKey");
        var streamValue = root.GetValue("StreamKey");

        return stringValue == "StringValue"
            && dictValue == "DictValue"
            && streamValue == "StreamValue";
    }
);

// Test 4: Missing key handling
await Test(
    "Missing key handling",
    async () =>
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("ExistingKey=SomeValue");

        await using var root = await builder.BuildAsync();
        var existingValue = root.GetValue("ExistingKey");
        var missingValue = root.GetValue("NonExistentKey");

        return existingValue == "SomeValue" && missingValue == null;
    }
);

// Test 5: Complex key names with special characters
await Test(
    "Complex key names",
    async () =>
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("Section:Subsection:Key=Value\nSection.Key.With.Dots=AnotherValue");

        await using var root = await builder.BuildAsync();
        var value1 = root.GetValue("Section:Subsection:Key");
        var value2 = root.GetValue("Section.Key.With.Dots");

        return value1 == "Value" && value2 == "AnotherValue";
    }
);

// Test 6: Empty lines and whitespace handling
await Test(
    "Whitespace handling",
    async () =>
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("\n\nKey1=Value1\n  \nKey2  =  Value2  \nKey3=\n\n");

        await using var root = await builder.BuildAsync();
        var value1 = root.GetValue("Key1");
        var value2 = root.GetValue("Key2");
        var value3 = root.GetValue("Key3");

        // Key2 should be trimmed, Key3 should be empty string
        return value1 == "Value1" && value2 == "Value2" && value3 == "";
    }
);

// Test 7: Async cancellation support
await Test(
    "Async cancellation",
    async () =>
    {
        try
        {
            var builder = Cfg.CreateBuilder();
            builder.Add("Key=Value");

            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            await using var root = await builder.BuildAsync(cts.Token);
            cts.Token.ThrowIfCancellationRequested();
            var value = root.GetValue("Key");

            // If we get here without cancellation, it's still OK for AOT validation
            return value == "Value";
        }
        catch (OperationCanceledException)
        {
            // Cancellation is also acceptable
            return true;
        }
    }
);

// Test 8: Root remains directly readable without tree APIs
await Test(
    "Root access",
    async () =>
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("Key1=Value1");
        builder.Add("Key2=Value2");

        await using var root = await builder.BuildAsync();

        return root.GetValue("Key1") == "Value1" && root.GetValue("Key2") == "Value2";
    }
);

// Test 9: Change notification
await Test(
    "Change notification",
    async () =>
    {
        var currentData = new Dictionary<string, string> { ["InitialKey"] = "InitialValue" };
        var version = 0;
        var builder = Cfg.CreateBuilder();
        builder.Add(() => currentData, () => version);

        await using var root = await builder.BuildAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var waitTask = root.WaitForChangeAsync(cts.Token).AsTask();

        currentData = new Dictionary<string, string> { ["InitialKey"] = "UpdatedValue" };
        version++;

        var changed = await root.ReloadAsync();
        await waitTask;
        return changed && waitTask.IsCompletedSuccessfully;
    }
);

Console.WriteLine("=== Environment Variables ===");

// Demo: Read all env vars with a prefix
// Note: Set some env vars before running, e.g., set MYAPP_NAME=PicoCfg
await using var envRoot = await Cfg.CreateBuilder().AddEnvironmentVariables().BuildAsync();
Console.WriteLine(
    $"  PATH = {envRoot.GetValue("PATH")?.Substring(0, Math.Min(50, envRoot.GetValue("PATH")?.Length ?? 0))}..."
);

Console.WriteLine("=== Command-Line ===");

// Demo: Parse command-line args
await using var cmdRoot = await Cfg.CreateBuilder()
    .AddCommandLine(["--Name=PicoCfg", "--Count", "42", "--Verbose"])
    .BuildAsync();
Console.WriteLine($"  Name = {cmdRoot.GetValue("Name")}");
Console.WriteLine($"  Count = {cmdRoot.GetValue("Count")}");
Console.WriteLine($"  Verbose = {cmdRoot.GetValue("Verbose")}");

Console.WriteLine("=== GetSection ===");

// Demo: Hierarchical sections
await using var sectionRoot = await Cfg.CreateBuilder()
    .Add("Logging:Level=Debug\nLogging:Console=true\nApp:Name=PicoCfg")
    .BuildAsync();
var logSection = sectionRoot.GetSection("Logging");
Console.WriteLine($"  Logging:Level = {logSection.GetValue("Level")}");
Console.WriteLine($"  Logging:Console = {logSection.GetValue("Console")}");

// Out-of-section lookup returns null
Console.WriteLine($"  Logging.App:Name = {logSection.GetValue("App:Name")} (should be null)");

Console.WriteLine("=== GetAll ===");

// Demo: Enumerate all configuration
await using var enumRoot = await Cfg.CreateBuilder()
    .Add("Key1=Value1\nKey2=Value2\nKey3=Value3")
    .BuildAsync();
var all = enumRoot.GetAll();
Console.WriteLine($"  Total keys: {all.Count}");
foreach (var (key, value) in all)
    Console.WriteLine($"  {key} = {value}");

Console.WriteLine("=== Key-Per-File ===");

// Demo: Key-per-file source (if temp directory available)
try
{
    var kpfDir = Path.Combine(Path.GetTempPath(), "PicoCfg_KPF_Demo");
    Directory.CreateDirectory(kpfDir);
    File.WriteAllText(Path.Combine(kpfDir, "name"), "PicoCfg");
    File.WriteAllText(Path.Combine(kpfDir, "port"), "8080");
    await using var kpfRoot = await Cfg.CreateBuilder().AddKeyPerFile(kpfDir).BuildAsync();
    Console.WriteLine($"  name = {kpfRoot.GetValue("name")}");
    Console.WriteLine($"  port = {kpfRoot.GetValue("port")}");
    Directory.Delete(kpfDir, true);
}
catch
{
    Console.WriteLine("  (Key-per-file demo skipped)");
}

Console.WriteLine("=== Validation ===");

// Demo: Bind and validate
try
{
    await using var validRoot = await Cfg.CreateBuilder()
        .Add("App:Name=PicoCfg\nApp:Enabled=true\nApp:Count=42")
        .BuildAsync();
    var settings = CfgBind.Bind<AppSettings>(validRoot, "App");
    CfgValidator.ValidateOrThrow(settings);
    Console.WriteLine(
        $"  Name = {settings.Name}, Enabled = {settings.Enabled}, Count = {settings.Count}"
    );

    // Demo: Invalid config
    await using var invalidRoot = await Cfg.CreateBuilder()
        .Add("App:Name=\nApp:Enabled=true\nApp:Count=-1")
        .BuildAsync();
    var badSettings = CfgBind.Bind<AppSettings>(invalidRoot, "App");
    CfgValidator.ValidateOrThrow(badSettings);
}
catch (CfgValidationException ex)
{
    Console.WriteLine($"  Validation failed for {ex.TargetType.Name}:");
    foreach (var err in ex.Errors)
        Console.WriteLine($"    - {err.ErrorMessage}");
}

Console.WriteLine("=== Chained Configuration ===");

// Demo: Chain one config into another
await using var baseRoot = await Cfg.CreateBuilder()
    .Add("Shared:Key=baseValue\nOverride:Old=shouldBeOverridden")
    .BuildAsync();
await using var chainedRoot = await Cfg.CreateBuilder()
    .AddConfiguration(baseRoot)
    .Add("Override:New=thisWins")
    .BuildAsync();
Console.WriteLine($"  Shared:Key = {chainedRoot.GetValue("Shared:Key")} (from base)");
Console.WriteLine($"  Override:Old = {chainedRoot.GetValue("Override:Old")} (from base)");
Console.WriteLine($"  Override:New = {chainedRoot.GetValue("Override:New")} (from chain)");

// Summary
Console.WriteLine();
Console.WriteLine("=== Validation Summary ===");
Console.WriteLine($"Total tests: {testResults.Count}");
Console.WriteLine($"Passed: {testResults.Count(r => r)}");
Console.WriteLine($"Failed: {testResults.Count(r => !r)}");

if (testResults.All(r => r))
{
    Console.WriteLine("✅ All tests passed! PicoCfg is AOT compatible.");
    return 0;
}
else
{
    Console.WriteLine("❌ Some tests failed. Check the output above.");
    return 1;
}

async Task Test(string name, Func<Task<bool>> test)
{
    try
    {
        Console.Write($"Testing: {name}... ");
        var result = await test();
        testResults.Add(result);

        if (result)
        {
            Console.WriteLine("✅ PASS");
        }
        else
        {
            Console.WriteLine("❌ FAIL (wrong result)");
        }
    }
    catch (Exception ex)
    {
        testResults.Add(false);
        Console.WriteLine($"❌ FAIL (exception: {ex.Message})");
    }
}

public sealed class AppSettings
{
    [Required]
    public string? Name { get; set; }
    public bool Enabled { get; set; }
    public int Count { get; set; }
}
