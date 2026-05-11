namespace PicoCfg.Tests;

public class CfgBuilderExtensionsTests
{
    [Test]
    public async Task Add_WithNullStreamFactory_ThrowsArgumentNullException()
    {
        var builder = Cfg.CreateBuilder();

        await Assert.That(() => builder.Add((Func<Stream>)null!)).Throws<ArgumentNullException>();
    }

    [Test]
    public async Task Add_WithStreamFactory_BuildAsyncPublishesParsedValues()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add(() => new MemoryStream(Encoding.UTF8.GetBytes("alpha=1\nbeta=2")));

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
    public async Task Add_WithStringContentAndExplicitEncoding_ForwardsEncodedBytesToTheParser()
    {
        byte[]? observedBytes = null;
        var builder = Cfg
            .CreateBuilder()
            .WithStreamParser(async (stream, ct) =>
            {
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, ct);
                observedBytes = memory.ToArray();
                return [];
            });

        builder.Add("key=value", Encoding.Unicode);

        await using var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("key")).IsNull();
        await Assert.That(observedBytes).IsNotNull();
        await Assert.That(observedBytes!.Length).IsGreaterThan(2);
        await Assert.That((int)observedBytes[0]).IsEqualTo(0xFF);
        await Assert.That((int)observedBytes[1]).IsEqualTo(0xFE);
    }

    [Test]
    public async Task Add_WithStringContentAndUnchangedVersionStamp_ReloadSkipsReparseAndRetainsPublishedValue()
    {
        var parserCalls = 0;
        var builder = Cfg
            .CreateBuilder()
            .WithStreamParser(async (stream, ct) =>
            {
                parserCalls++;
                return await CfgBuilder.CreateDefaultStreamParser()(stream, ct);
            });

        builder.Add("key=value1", versionStampFactory: () => 1);

        await using var root = await builder.BuildAsync();
        var changed = await root.ReloadAsync();

        await Assert.That(changed).IsFalse();
        await Assert.That(parserCalls).IsEqualTo(1);
        await Assert.That(root.GetValue("key")).IsEqualTo("value1");
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
        var data = new Dictionary<string, string>
        {
            ["key"] = "before",
        };
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
        var data = new Dictionary<string, string>
        {
            ["key"] = "before",
        };
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

        await Assert.That(() => builder.Add((IDictionary<string, string>)null!))
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

        await Assert.That(() => builder.Add((Func<IEnumerable<KeyValuePair<string, string>>>)null!))
            .Throws<ArgumentNullException>();
    }
}
