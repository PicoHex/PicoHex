namespace PicoCfg.DI.Tests;

public sealed class CfgOptionsExtensionsTests
{
    [Before(Class)]
    public static void SetupBinding() => CfgBindTestHelper.RegisterOptionsTargetBinding();

    public sealed class OptionsTarget
    {
        public string? Name { get; set; }
        public int Count { get; set; }
    }

    [Test]
    public async Task RegisterCfgOptionsSingleton_ResolvesSameInstance()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add(
            new Dictionary<string, string>
            {
                ["DIOptions:Name"] = "SingletonDI",
                ["DIOptions:Count"] = "99",
            }
        );

        await using var root = await builder.BuildAsync();
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        container.RegisterCfgRoot(root).RegisterCfgOptionsSingleton<OptionsTarget>("DIOptions");

        await using var scope = container.CreateScope();

        var first = scope.GetService<ICfgOptions<OptionsTarget>>();
        var second = scope.GetService<ICfgOptions<OptionsTarget>>();

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(first.Value.Name).IsEqualTo("SingletonDI");
        await Assert.That(first.Value.Count).IsEqualTo(99);
        await Assert.That(second.Value).IsSameReferenceAs(first.Value);
    }

    [Test]
    public async Task RegisterCfgOptionsScoped_ResolvesPerScope()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add(
            new Dictionary<string, string>
            {
                ["ScopedOpts:Name"] = "ScopedDI",
                ["ScopedOpts:Count"] = "50",
            }
        );

        await using var root = await builder.BuildAsync();
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        container.RegisterCfgRoot(root).RegisterCfgOptionsScoped<OptionsTarget>("ScopedOpts");

        await using var firstScope = container.CreateScope();
        await using var secondScope = container.CreateScope();

        var first = firstScope.GetService<ICfgOptions<OptionsTarget>>();
        var second = secondScope.GetService<ICfgOptions<OptionsTarget>>();

        await Assert.That(second).IsNotSameReferenceAs(first);
        await Assert.That(first.Value.Name).IsEqualTo("ScopedDI");
        await Assert.That(second.Value.Name).IsEqualTo("ScopedDI");
    }
}
