namespace PicoCfg.DI.Tests;

public sealed class SvcContainerExtensionsTests
{
    [Test]
    public async Task RegisterCfgRoot_RegistersRootWithoutDefaultSnapshotService()
    {
        var state = CreateSettingsData("Before", 1);
        var version = 0;

        await using var root = await CreateRootAsync(() => state, () => version);
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        container.RegisterCfgRoot(root);

        await using var scope = container.CreateScope();
        var resolvedRoot = scope.GetService<ICfgRoot>();
        var cfgBefore = scope.GetService<ICfg>();

        await Assert.That(resolvedRoot).IsSameReferenceAs(root);
        await Assert.That(cfgBefore.GetValue("App:Name")).IsEqualTo("Before");
        await Assert.That(() => scope.GetService<ICfgSnapshot>()).Throws<PicoDiException>();

        state = CreateSettingsData("After", 2);
        version++;
        await root.ReloadAsync();

        var cfgAfter = scope.GetService<ICfg>();

        await Assert.That(cfgAfter.GetValue("App:Name")).IsEqualTo("After");
    }

    [Test]
    public async Task RegisterCfgTransient_BindsCurrentSnapshotPerResolution()
    {
        var state = CreateSettingsData("Before", 1);
        var version = 0;

        await using var root = await CreateRootAsync(() => state, () => version);
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        container.RegisterCfgRoot(root).RegisterCfgTransient<AppSettings>("App");

        await using var scope = container.CreateScope();
        var before = scope.GetService<AppSettings>();

        state = CreateSettingsData("After", 2);
        version++;
        await root.ReloadAsync();

        var after = scope.GetService<AppSettings>();

        await Assert.That(before).IsNotSameReferenceAs(after);
        await Assert.That(before.Name).IsEqualTo("Before");
        await Assert.That(after.Name).IsEqualTo("After");
        await Assert.That(after.Count).IsEqualTo(2);
    }

    [Test]
    public async Task RegisterCfgScoped_BindsOncePerScope()
    {
        var state = CreateSettingsData("Before", 1);
        var version = 0;

        await using var root = await CreateRootAsync(() => state, () => version);
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        container.RegisterCfgRoot(root).RegisterCfgScoped<AppSettings>("App");

        await using var firstScope = container.CreateScope();
        var first = firstScope.GetService<AppSettings>();
        var sameScopeAgain = firstScope.GetService<AppSettings>();

        state = CreateSettingsData("After", 2);
        version++;
        await root.ReloadAsync();

        var sameScopeAfterReload = firstScope.GetService<AppSettings>();

        await using var secondScope = container.CreateScope();
        var nextScope = secondScope.GetService<AppSettings>();

        await Assert.That(sameScopeAgain).IsSameReferenceAs(first);
        await Assert.That(sameScopeAfterReload).IsSameReferenceAs(first);
        await Assert.That(first.Name).IsEqualTo("Before");
        await Assert.That(nextScope).IsNotSameReferenceAs(first);
        await Assert.That(nextScope.Name).IsEqualTo("After");
    }

    [Test]
    public async Task RegisterCfgSingleton_BindsOnceForContainerLifetime()
    {
        var state = CreateSettingsData("Before", 1);
        var version = 0;

        await using var root = await CreateRootAsync(() => state, () => version);
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        container.RegisterCfgRoot(root).RegisterCfgSingleton<AppSettings>("App");

        await using var firstScope = container.CreateScope();
        var first = firstScope.GetService<AppSettings>();

        state = CreateSettingsData("After", 2);
        version++;
        await root.ReloadAsync();

        await using var secondScope = container.CreateScope();
        var second = secondScope.GetService<AppSettings>();

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(second.Name).IsEqualTo("Before");
        await Assert.That(second.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RegisterCfgTransient_WithoutRegisteredRootOrSnapshot_FailsFast()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        container.RegisterCfgTransient<AppSettings>("App");

        await using var scope = container.CreateScope();
        var thrown = await Assert
            .That(() => scope.GetService<AppSettings>())
            .Throws<InvalidOperationException>();

        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown.Message).Contains("RegisterCfgRoot(...)");
    }

    private static async Task<ICfgRoot> CreateRootAsync(
        Func<IReadOnlyDictionary<string, string>> dataFactory,
        Func<int> versionFactory
    ) => await Cfg.CreateBuilder().Add(() => dataFactory(), () => versionFactory()).BuildAsync();

    private static Dictionary<string, string> CreateSettingsData(string name, int count) =>
        new() { ["App:Name"] = name, ["App:Count"] = count.ToString(), };

    public sealed class AppSettings
    {
        public string? Name { get; set; }
        public int Count { get; set; }
    }
}
