// PicoCfg + PicoSerDe — Structured Config Source Demo
// Demonstrates JSON, YAML, INI, and TOML as first-class config sources in PicoCfg.
// All formats are flattened into the same key:value model using ':' as hierarchy separator.

Console.WriteLine("=== PicoCfg.SerDe — Structured Configuration Sources ===");
Console.WriteLine();

// ── Create temp config files for file-based demos ──

var tempDir = Path.Combine(Path.GetTempPath(), "PicoCfg_SerDe_Demo");
Directory.CreateDirectory(tempDir);

var jsonFile = Path.Combine(tempDir, "appsettings.json");
var yamlFile = Path.Combine(tempDir, "config.yaml");
var iniFile = Path.Combine(tempDir, "config.ini");
var tomlFile = Path.Combine(tempDir, "config.toml");

File.WriteAllText(jsonFile, """{"App":{"Name":"PicoCfg","Port":8080},"Debug":true}""");
File.WriteAllText(yamlFile, "Server:\n  Host: localhost\n  Port: \"5432\"\n");
File.WriteAllText(iniFile, "[Database]\nServer=db.example.com\nPort=5432\n\n[App]\nName=PicoCfg\n");
File.WriteAllText(tomlFile, "[Server]\nHost = \"example.com\"\nPort = 8080\n");

Console.WriteLine($"Temp config directory: {tempDir}");
Console.WriteLine();

var testResults = new List<bool>();

// ── JSON ──

await Test(
    "JSON — simple object",
    async () =>
    {
        var root = await Cfg.CreateBuilder()
            .AddJson("""{"App":{"Name":"PicoCfg","Port":8080},"Debug":true}""")
            .BuildAsync();
        return root.GetValue("App:Name") == "PicoCfg"
            && root.GetValue("App:Port") == "8080"
            && root.GetValue("Debug") == "true";
    }
);

await Test(
    "JSON — nested objects",
    async () =>
    {
        var root = await Cfg.CreateBuilder()
            .AddJson("""{"Logging":{"Level":"Debug","File":{"Path":"/logs/app.log"}}}""")
            .BuildAsync();
        return root.GetValue("Logging:Level") == "Debug"
            && root.GetValue("Logging:File:Path") == "/logs/app.log";
    }
);

await Test(
    "JSON — last source wins",
    async () =>
    {
        var root = await Cfg.CreateBuilder()
            .AddJson("""{"Key":"first"}""")
            .AddJson("""{"Key":"second"}""")
            .BuildAsync();
        return root.GetValue("Key") == "second";
    }
);

// ── YAML ──

await Test(
    "YAML — simple mapping",
    async () =>
    {
        var root = await Cfg.CreateBuilder()
            .AddYaml("Name: PicoCfg\nVersion: \"2.0\"")
            .BuildAsync();
        return root.GetValue("Name") == "PicoCfg" && root.GetValue("Version") == "2.0";
    }
);

await Test(
    "YAML — nested mapping",
    async () =>
    {
        var root = await Cfg.CreateBuilder()
            .AddYaml("Server:\n  Host: localhost\n  Port: \"5432\"")
            .BuildAsync();
        return root.GetValue("Server:Host") == "localhost"
            && root.GetValue("Server:Port") == "5432";
    }
);

// ── INI ──

await Test(
    "INI — sections",
    async () =>
    {
        var root = await Cfg.CreateBuilder()
            .AddIni("[Database]\nServer=db.example.com\nPort=5432\n[App]\nName=PicoCfg")
            .BuildAsync();
        return root.GetValue("Database:Server") == "db.example.com"
            && root.GetValue("Database:Port") == "5432"
            && root.GetValue("App:Name") == "PicoCfg";
    }
);

await Test(
    "INI — nested sections (dot notation)",
    async () =>
    {
        var root = await Cfg.CreateBuilder()
            .AddIni("[Parent]\nKey=value1\n[Parent.Child]\nKey=value2")
            .BuildAsync();
        return root.GetValue("Parent:Key") == "value1"
            && root.GetValue("Parent:Child:Key") == "value2";
    }
);

// ── TOML ──

await Test(
    "TOML — tables",
    async () =>
    {
        var root = await Cfg.CreateBuilder()
            .AddToml("[Server]\nHost = \"example.com\"\nPort = 8080")
            .BuildAsync();
        return root.GetValue("Server:Host") == "example.com"
            && root.GetValue("Server:Port") == "8080";
    }
);

await Test(
    "TOML — mixed formats with priority",
    async () =>
    {
        var root = await Cfg.CreateBuilder()
            .AddJson("""{"Shared":"from-json","JsonOnly":"yes"}""")
            .AddYaml("Shared: from-yaml\nYamlOnly: yes")
            .AddIni("[Section]\nShared=from-ini\nIniOnly=yes")
            .AddToml("Shared = \"from-toml\"\nTomlOnly = \"yes\"")
            .BuildAsync();

        return root.GetValue("Shared") == "from-toml" // last source wins
            && root.GetValue("JsonOnly") == "yes"
            && root.GetValue("YamlOnly") == "yes"
            && root.GetValue("Section:IniOnly") == "yes"
            && root.GetValue("TomlOnly") == "yes";
    }
);

// ── File-based sources ──

await Test(
    "File — AddJsonFile from disk",
    async () =>
    {
        var root = await Cfg.CreateBuilder().AddJsonFile(jsonFile).BuildAsync();
        return root.GetValue("App:Name") == "PicoCfg"
            && root.GetValue("App:Port") == "8080"
            && root.GetValue("Debug") == "true";
    }
);

await Test(
    "File — AddYamlFile from disk",
    async () =>
    {
        var root = await Cfg.CreateBuilder().AddYamlFile(yamlFile).BuildAsync();
        return root.GetValue("Server:Host") == "localhost"
            && root.GetValue("Server:Port") == "5432";
    }
);

await Test(
    "File — AddIniFile from disk",
    async () =>
    {
        var root = await Cfg.CreateBuilder().AddIniFile(iniFile).BuildAsync();
        return root.GetValue("Database:Server") == "db.example.com"
            && root.GetValue("Database:Port") == "5432"
            && root.GetValue("App:Name") == "PicoCfg";
    }
);

await Test(
    "File — AddTomlFile from disk",
    async () =>
    {
        var root = await Cfg.CreateBuilder().AddTomlFile(tomlFile).BuildAsync();
        return root.GetValue("Server:Host") == "example.com"
            && root.GetValue("Server:Port") == "8080";
    }
);

// ── Summary ──

Directory.Delete(tempDir, true);

Console.WriteLine();
Console.WriteLine("=== Results ===");
Console.WriteLine($"Total: {testResults.Count}");
Console.WriteLine($"Passed: {testResults.Count(r => r)}");
Console.WriteLine($"Failed: {testResults.Count(r => !r)}");

return testResults.All(r => r) ? 0 : 1;

async Task Test(string name, Func<Task<bool>> test)
{
    try
    {
        Console.Write($"  {name}... ");
        var result = await test();
        testResults.Add(result);
        Console.WriteLine(result ? "PASS" : "FAIL (wrong result)");
    }
    catch (Exception ex)
    {
        testResults.Add(false);
        Console.WriteLine($"FAIL ({ex.GetType().Name}: {ex.Message})");
    }
}
