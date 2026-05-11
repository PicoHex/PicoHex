namespace PicoCfg.Tests;

public class EnvCfgTests
{
    private static string UniqueEnvName() => $"PICOCFG_TEST_{Guid.NewGuid():N}";

    [Test]
    public async Task AddEnvironmentVariables_WithoutPrefix_ReturnsAllVars()
    {
        var key = UniqueEnvName();
        try
        {
            Environment.SetEnvironmentVariable(key, "expected_value");

            await using var root = await Cfg
                .CreateBuilder()
                .AddEnvironmentVariables()
                .BuildAsync();

            await Assert.That(root.GetValue(key)).IsEqualTo("expected_value");
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Test]
    public async Task AddEnvironmentVariables_WithPrefix_FiltersCorrectly()
    {
        try
        {
            Environment.SetEnvironmentVariable("MYTEST_Foo", "bar");
            Environment.SetEnvironmentVariable("MYTEST_Baz", "qux");
            Environment.SetEnvironmentVariable("OTHER_X", "y");

            await using var root = await Cfg
                .CreateBuilder()
                .AddEnvironmentVariables("MYTEST_")
                .BuildAsync();

            await Assert.That(root.GetValue("Foo")).IsEqualTo("bar");
            await Assert.That(root.GetValue("Baz")).IsEqualTo("qux");
            await Assert.That(root.GetValue("OTHER_X")).IsNull();
        }
        finally
        {
            Environment.SetEnvironmentVariable("MYTEST_Foo", null);
            Environment.SetEnvironmentVariable("MYTEST_Baz", null);
            Environment.SetEnvironmentVariable("OTHER_X", null);
        }
    }

    [Test]
    public async Task AddEnvironmentVariables_DoubleUnderscore_BecomesColon()
    {
        try
        {
            Environment.SetEnvironmentVariable("MYTEST_DB__HOST", "localhost");

            await using var root = await Cfg
                .CreateBuilder()
                .AddEnvironmentVariables("MYTEST_")
                .BuildAsync();

            await Assert.That(root.GetValue("DB:HOST")).IsEqualTo("localhost");
        }
        finally
        {
            Environment.SetEnvironmentVariable("MYTEST_DB__HOST", null);
        }
    }

    [Test]
    public async Task AddEnvironmentVariables_Empty_ReturnsNulls()
    {
        var uniquePrefix = $"NONEXISTENT_{Guid.NewGuid():N}_";

        await using var root = await Cfg
            .CreateBuilder()
            .AddEnvironmentVariables(uniquePrefix)
            .BuildAsync();

        await Assert.That(root.GetValue("anything")).IsNull();
    }
}
