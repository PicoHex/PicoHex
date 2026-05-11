namespace PicoCfg.Tests;

public sealed class CfgOptionsTests
{
    [Before(Class)]
    public static void SetupBinding() => CfgBindTestHelper.RegisterTestClassBinding();

    public sealed class TestClass
    {
        public string? Name { get; set; }
        public int Count { get; set; }
    }

    [Test]
    public async Task CfgOptions_Singleton_CachesValue()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add(new Dictionary<string, string>
        {
            ["Options:Name"] = "Singleton",
            ["Options:Count"] = "42",
        });

        await using var root = await builder.BuildAsync();

        var options = new CfgOptions<TestClass>(root, "Options");

        var first = options.Value;
        var second = options.Value;

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(first.Name).IsEqualTo("Singleton");
        await Assert.That(first.Count).IsEqualTo(42);
    }

    [Test]
    public async Task CfgOptionsSnapshot_Rebinds_OnEveryAccess()
    {
        var data = new Dictionary<string, string>
        {
            ["Snapshot:Name"] = "Before",
            ["Snapshot:Count"] = "1",
        };
        var builder = Cfg.CreateBuilder();
        builder.Add(data);

        await using var root = await builder.BuildAsync();

        var snapshot = new CfgOptionsSnapshot<TestClass>(root, "Snapshot");

        var first = snapshot.Value;
        var second = snapshot.Value;

        await Assert.That(second).IsNotSameReferenceAs(first);
        await Assert.That(first.Name).IsEqualTo("Before");
        await Assert.That(first.Count).IsEqualTo(1);
        await Assert.That(second.Name).IsEqualTo("Before");
        await Assert.That(second.Count).IsEqualTo(1);

        // Mutate backing data, reload, and verify the snapshot sees the updated values
        data["Snapshot:Name"] = "After";
        data["Snapshot:Count"] = "2";
        await root.ReloadAsync();

        var after = snapshot.Value;

        await Assert.That(after).IsNotSameReferenceAs(first);
        await Assert.That(after.Name).IsEqualTo("After");
        await Assert.That(after.Count).IsEqualTo(2);
    }
}
