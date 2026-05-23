namespace PicoCfg.Tests;

public class CfgBuilderExtensionsTests
{
    [Test]
    public async Task Add_WithNullStreamFactory_ThrowsArgumentNullException()
    {
        var builder = Cfg.CreateBuilder();

        await Assert
            .That(() => builder.Add((Func<CancellationToken, ValueTask<Stream>>)null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Add_WithStreamFactory_BuildAsyncPublishesParsedValues()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add(
            ct =>
                ValueTask.FromResult<Stream>(
                    new MemoryStream(Encoding.UTF8.GetBytes("alpha=1\nbeta=2"))
                )
        );

        await using var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("alpha")).IsEqualTo("1");
        await Assert.That(root.GetValue("beta")).IsEqualTo("2");
    }

    [Test]
    public async Task Add_WithStringContent_BuildAsyncPublishesParsedValues()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add(" alpha = a=b=c \n invalid \n gamma = 3 ");

        await using var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("alpha")).IsEqualTo("a=b=c");
        await Assert.That(root.GetValue("gamma")).IsEqualTo("3");
        await Assert.That(root.GetValue("invalid")).IsNull();
    }

    [Test]
    public async Task Add_WithDictionaryData_PreservesRawValuesWithoutReparsing()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add(
            new Dictionary<string, string>
            {
                ["withEquals"] = "a=b=c",
                ["withNewLine"] = "line1\nline2",
            }
        );

        await using var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("withEquals")).IsEqualTo("a=b=c");
        await Assert.That(root.GetValue("withNewLine")).IsEqualTo("line1\nline2");
    }

    [Test]
    public async Task Add_WithDictionaryDataWithoutVersionStamp_ReloadPublishesMutatedDictionaryValues()
    {
        var data = new Dictionary<string, string> { ["key"] = "before", };
        var builder = Cfg.CreateBuilder();
        builder.Add(data);

        await using var root = await builder.BuildAsync();
        data["key"] = "after";

        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await Assert.That(root.GetValue("key")).IsEqualTo("after");
    }

    [Test]
    public async Task Add_WithDictionaryDataAndUnchangedVersionStamp_ReloadSkipsMutatedDictionaryValues()
    {
        var data = new Dictionary<string, string> { ["key"] = "before", };
        var builder = Cfg.CreateBuilder();
        builder.Add(data, () => 1);

        await using var root = await builder.BuildAsync();
        data["key"] = "after";

        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsFalse();
        await Assert.That(root.GetValue("key")).IsEqualTo("before");
    }

    [Test]
    public async Task Add_WithNullDictionary_ThrowsArgumentNullException()
    {
        var builder = Cfg.CreateBuilder();

        await Assert
            .That(() => builder.Add((IDictionary<string, string>)null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Add_WithDataFactory_ReloadPublishesUpdatedFactoryValues()
    {
        var currentValue = "before";
        var builder = Cfg.CreateBuilder();
        builder.Add(() => new Dictionary<string, string> { ["key"] = currentValue });

        await using var root = await builder.BuildAsync();
        currentValue = "after";

        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsTrue();
        await Assert.That(root.GetValue("key")).IsEqualTo("after");
    }

    [Test]
    public async Task Add_WithNullDataFactory_ThrowsArgumentNullException()
    {
        var builder = Cfg.CreateBuilder();

        await Assert
            .That(() => builder.Add((Func<IEnumerable<KeyValuePair<string, string>>>)null!))
            .Throws<ArgumentNullException>();
    }
}
