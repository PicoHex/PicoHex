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
}
