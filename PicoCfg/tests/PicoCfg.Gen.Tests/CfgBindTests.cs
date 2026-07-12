namespace PicoCfg.Gen.Tests;

public sealed class CfgBindTests
{
    [Test]
    public async Task CfgBind_LivesInPicoCfgAssembly()
    {
        await Assert.That(typeof(CfgBind).Assembly).IsSameReferenceAs(typeof(Cfg).Assembly);
    }

    [Test]
    public async Task Bind_BindsFromRootAsCfg()
    {
        await using var root = await Cfg.CreateBuilder()
            .Add(new Dictionary<string, string> { ["Name"] = "PicoCfg", ["Count"] = "42" })
            .BuildAsync();

        var settings = CfgBind.Bind<PicoCfgBindRuntimeTests.FlatSettings>((ICfg)root);

        await Assert.That(settings.Name).IsEqualTo("PicoCfg");
        await Assert.That(settings.Count).IsEqualTo(42);
    }

    [Test]
    public async Task Bind_BindsFromRootSnapshot()
    {
        await using var root = await Cfg.CreateBuilder()
            .Add(new Dictionary<string, string> { ["Name"] = "Runtime", ["Count"] = "7" })
            .BuildAsync();

        var settings = CfgBind.Bind<PicoCfgBindRuntimeTests.FlatSettings>(root);

        await Assert.That(settings.Name).IsEqualTo("Runtime");
        await Assert.That(settings.Count).IsEqualTo(7);
    }

    [Test]
    public async Task Bind_BindsFromCfgWhenCfgIsRoot()
    {
        await using var root = await Cfg.CreateBuilder()
            .Add(new Dictionary<string, string> { ["Name"] = "Cfg", ["Count"] = "3" })
            .BuildAsync();

        ICfg cfg = root;

        var settings = CfgBind.Bind<PicoCfgBindRuntimeTests.FlatSettings>(cfg);

        await Assert.That(settings.Name).IsEqualTo("Cfg");
        await Assert.That(settings.Count).IsEqualTo(3);
    }

    [Test]
    public async Task Bind_FromNonSnapshotCfg_SucceedsWhenKeysExist()
    {
        ICfg cfg = new InlineCfg(
            new Dictionary<string, string> { ["Name"] = "Loose", ["Count"] = "5" }
        );

        var settings = CfgBind.Bind<PicoCfgBindRuntimeTests.FlatSettings>(cfg);

        await Assert.That(settings.Name).IsEqualTo("Loose");
        await Assert.That(settings.Count).IsEqualTo(5);
    }

    private sealed class InlineCfg(IReadOnlyDictionary<string, string> values) : ICfg
    {
        public bool TryGetValue(string path, out string? value)
        {
            if (values.TryGetValue(path, out var resolved))
            {
                value = resolved;
                return true;
            }

            value = null;
            return false;
        }
    }

    // Partial binding: struct property that fails analysis doesn't block scalars
    public struct TestPoint
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public sealed class ConfigWithStruct
    {
        public string Name { get; set; } = "";
        public int Port { get; set; }
        public TestPoint? Pos { get; set; }
    }

    [Test]
    public async Task PartialBinding_StructPropertyDoesNotBlockScalars()
    {
        await using var root = await Cfg.CreateBuilder()
            .Add(new Dictionary<string, string> { ["Name"] = "app", ["Port"] = "8080" })
            .BuildAsync();

        var settings = CfgBind.Bind<ConfigWithStruct>(root);

        await Assert.That(settings.Name).IsEqualTo("app");
        await Assert.That(settings.Port).IsEqualTo(8080);
    }

    [Test]
    public async Task Bind_NestedNonCircularBindings_ResolvesChildProperties()
    {
        await using var root = await Cfg.CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Name"] = "Root",
                    ["Inner:Value"] = "nested-value",
                    ["Inner:Count"] = "99",
                }
            )
            .BuildAsync();

        var settings = CfgBind.Bind<PicoCfgBindRuntimeTests.OuterSettings>(root);

        await Assert.That(settings.Name).IsEqualTo("Root");
        await Assert.That(settings.Inner).IsNotNull();
        await Assert.That(settings.Inner!.Value).IsEqualTo("nested-value");
        await Assert.That(settings.Inner.Count).IsEqualTo(99);
    }

    // --- Dictionary<string, NestedType> binding (settable properties) ---

    public sealed class ProviderEntry
    {
        public string? Name { get; set; }
        public int Port { get; set; }
    }

    public sealed class DictAgentConfig
    {
        public string? Model { get; set; }
        public bool Te { get; set; }
        public int Mt { get; set; }
        public Dictionary<string, ProviderEntry> Providers { get; set; } = new();
    }

    [Test]
    public async Task Bind_DictOfNestedType_ResolvesKeysAndValues()
    {
        // Uses the dictionary key-value indexed format:
        //   Section:0:Key, Section:0:Value:Name, Section:0:Value:Port, ...
        await using var root = await Cfg.CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Model"] = "gpt4",
                    ["Te"] = "false",
                    ["Mt"] = "100",
                    ["Providers:0:Key"] = "primary",
                    ["Providers:0:Value:Name"] = "main",
                    ["Providers:0:Value:Port"] = "8080",
                    ["Providers:1:Key"] = "secondary",
                    ["Providers:1:Value:Name"] = "backup",
                    ["Providers:1:Value:Port"] = "8081",
                }
            )
            .BuildAsync();

        var config = CfgBind.Bind<DictAgentConfig>(root);

        await Assert.That(config.Model).IsEqualTo("gpt4");
        await Assert.That(config.Te).IsFalse();
        await Assert.That(config.Mt).IsEqualTo(100);
        await Assert.That(config.Providers).IsNotNull();
        await Assert.That(config.Providers.Count).IsEqualTo(2);
        await Assert.That(config.Providers["primary"].Name).IsEqualTo("main");
        await Assert.That(config.Providers["primary"].Port).IsEqualTo(8080);
        await Assert.That(config.Providers["secondary"].Name).IsEqualTo("backup");
        await Assert.That(config.Providers["secondary"].Port).IsEqualTo(8081);
    }

    // --- Dictionary<string, NestedType> with init-only properties ---

    public sealed class ProviderEntryInit
    {
        public string? Name { get; init; }
        public int Port { get; init; }
    }

    public sealed class DictAgentConfigInit
    {
        public string? Model { get; init; }
        public bool Te { get; init; }
        public int Mt { get; init; }
        public Dictionary<string, ProviderEntryInit> Providers { get; init; } = new();
    }

    [Test]
    public async Task Bind_DictOfNestedInitOnlyType_ResolvesKeysAndValues()
    {
        await using var root = await Cfg.CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Model"] = "gpt4",
                    ["Te"] = "false",
                    ["Mt"] = "100",
                    ["Providers:0:Key"] = "primary",
                    ["Providers:0:Value:Name"] = "main",
                    ["Providers:0:Value:Port"] = "8080",
                    ["Providers:1:Key"] = "secondary",
                    ["Providers:1:Value:Name"] = "backup",
                    ["Providers:1:Value:Port"] = "8081",
                }
            )
            .BuildAsync();

        var config = CfgBind.Bind<DictAgentConfigInit>(root);

        await Assert.That(config.Model).IsEqualTo("gpt4");
        await Assert.That(config.Te).IsFalse();
        await Assert.That(config.Mt).IsEqualTo(100);
        await Assert.That(config.Providers).IsNotNull();
        await Assert.That(config.Providers.Count).IsEqualTo(2);
        await Assert.That(config.Providers["primary"].Name).IsEqualTo("main");
        await Assert.That(config.Providers["primary"].Port).IsEqualTo(8080);
        await Assert.That(config.Providers["secondary"].Name).IsEqualTo("backup");
        await Assert.That(config.Providers["secondary"].Port).IsEqualTo(8081);
    }

    // --- Dictionary<string, string> regression ---

    public sealed class DictStringConfig
    {
        public string? Name { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    [Test]
    public async Task Bind_DictOfStringType_ResolvesKeysAndValues()
    {
        await using var root = await Cfg.CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Name"] = "app",
                    ["Metadata:0:Key"] = "env",
                    ["Metadata:0:Value"] = "production",
                    ["Metadata:1:Key"] = "region",
                    ["Metadata:1:Value"] = "us-east-1",
                }
            )
            .BuildAsync();

        var config = CfgBind.Bind<DictStringConfig>(root);

        await Assert.That(config.Name).IsEqualTo("app");
        await Assert.That(config.Metadata).IsNotNull();
        await Assert.That(config.Metadata.Count).IsEqualTo(2);
        await Assert.That(config.Metadata["env"]).IsEqualTo("production");
        await Assert.That(config.Metadata["region"]).IsEqualTo("us-east-1");
    }

    // --- CamelCase key → PascalCase property (case-insensitive lookup) ---

    public sealed class PnProviderEntry
    {
        public string ApiKey { get; set; } = "";
        public string? BaseUrl { get; set; }
        public string? ApiFormat { get; set; }
    }

    public sealed class PnAgentConfig
    {
        public string? Model { get; set; }
        public bool ThinkingEnabled { get; set; }
        public int? MaxTokens { get; set; }
        public Dictionary<string, PnProviderEntry> Providers { get; set; } = [];
    }

    [Test]
    public async Task Bind_CamelCaseKeys_BindsToPascalCaseProperties()
    {
        // JSON convention: camelCase keys; C# convention: PascalCase properties.
        // Gen should do case-insensitive lookup so binding succeeds.
        await using var root = await Cfg.CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["model"] = "gpt-4",
                    ["thinkingEnabled"] = "true",
                    ["maxTokens"] = "8192",
                    ["providers:0:Key"] = "openai",
                    ["providers:0:Value:apiKey"] = "sk-123",
                    ["providers:0:Value:baseUrl"] = "https://api.openai.com",
                }
            )
            .BuildAsync();

        var config = CfgBind.Bind<PnAgentConfig>(root);

        await Assert.That(config.Model).IsEqualTo("gpt-4");
        await Assert.That(config.ThinkingEnabled).IsTrue();
        await Assert.That(config.MaxTokens).IsEqualTo(8192);
        await Assert.That(config.Providers).IsNotNull();
        await Assert.That(config.Providers.Count).IsEqualTo(1);
        await Assert.That(config.Providers["openai"].ApiKey).IsEqualTo("sk-123");
        await Assert.That(config.Providers["openai"].BaseUrl).IsEqualTo("https://api.openai.com");
        await Assert.That(config.Providers["openai"].ApiFormat).IsNull();
    }

    // --- Section-scoped case-insensitive lookup (CfgSection fallback) ---

    /// <summary>
    /// Snapshot-like config that provides <see cref="ICfgSnapshot.GetAllValues"/>
    /// but does NOT have a case-insensitive TryGetValue fallback.
    /// Used to verify that <see cref="CfgSection.TryGetValue"/> provides its own
    /// fallback by scanning the parent's key set.
    /// </summary>
    private sealed class ExactOnlySnapshot(IReadOnlyDictionary<string, string> values)
        : ICfgSnapshot
    {
        public bool TryGetValue(string path, out string? value) =>
            values.TryGetValue(path, out value);

        public IReadOnlyDictionary<string, string> GetAllValues() => values;
    }

    [Test]
    public async Task GetSection_CamelCaseKeys_FindsViaCfgSectionFallback()
    {
        // ExactOnlySnapshot does NOT do case-insensitive TryGetValue.
        // CfgSection must provide its own fallback by scanning the parent's
        // GetAllValues() + filtering by section prefix (OrdinalIgnoreCase).
        var snapshot = new ExactOnlySnapshot(
            new Dictionary<string, string>
            {
                ["section:key"] = "value",
                ["section:nested:deep"] = "deep-value",
            }
        );
        var section = snapshot.GetSection("Section"); // PascalCase vs "section"

        await Assert.That(section.TryGetValue("key", out var val) ? val : null).IsEqualTo("value");
        await Assert
            .That(section.TryGetValue("nested:deep", out var nested) ? nested : null)
            .IsEqualTo("deep-value");
        // Exact match should still fail (case mismatch), fallback should find it
        await Assert.That(snapshot.TryGetValue("Section:key", out var _)).IsFalse();
    }
}
