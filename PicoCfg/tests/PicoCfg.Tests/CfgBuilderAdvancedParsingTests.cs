namespace PicoCfg.Tests;

public class CfgBuilderAdvancedParsingTests
{
    [Test]
    public async Task Add_WithQuotedValue_StripsQuotes()
    {
        // 🔴 RED: Currently quoted values include the quotes in the value.
        // After fix, quotes should be stripped.
        var builder = Cfg.CreateBuilder();
        builder.Add(@"Key=""Value With Spaces""");

        await using var root = await builder.BuildAsync();
        var val = root.GetValue("Key");

        await Assert.That(val).IsEqualTo("Value With Spaces");
    }

    [Test]
    public async Task Add_WithQuotedValueAndEquals_StripsQuotes()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add(@"Key=""Value=With=Equals""");

        await using var root = await builder.BuildAsync();
        var val = root.GetValue("Key");

        await Assert.That(val).IsEqualTo("Value=With=Equals");
    }

    [Test]
    public async Task Add_WithCommentLine_SkipsLine()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("Key=Value\n# this is a comment\nAnother=Thing");

        await using var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Key")).IsEqualTo("Value");
        await Assert.That(root.GetValue("Another")).IsEqualTo("Thing");
    }

    [Test]
    public async Task Add_WithDoubleSlashComment_SkipsLine()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("Key=Value\n// this is a comment\nAnother=Thing");

        await using var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Key")).IsEqualTo("Value");
        await Assert.That(root.GetValue("Another")).IsEqualTo("Thing");
    }

    [Test]
    public async Task Add_WithInlineComment_StripsComment()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("Key=Value  # comment after value");

        await using var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Key")).IsEqualTo("Value");
    }

    [Test]
    public async Task Add_WithInlineSlashComment_StripsComment()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("Key=Value  // comment after value");

        await using var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Key")).IsEqualTo("Value");
    }

    [Test]
    public async Task Add_WithHashInQuotedValue_DoesNotStrip()
    {
        // A hash inside a quoted value should NOT be treated as a comment
        var builder = Cfg.CreateBuilder();
        builder.Add(@"Key=""Value # with hash""");

        await using var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Key")).IsEqualTo("Value # with hash");
    }

    [Test]
    public async Task Add_WithQuotedValueAroundEquals_UsesFirstEquals()
    {
        // Key="val=ue" should parse key=Key, value=val=ue (quotes stripped)
        var builder = Cfg.CreateBuilder();
        builder.Add(@"Key=""val=ue""");

        await using var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Key")).IsEqualTo("val=ue");
    }

    [Test]
    public async Task Add_WithEmptyQuotedValue_ReturnsEmptyString()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add(@"Key=""""");

        await using var root = await builder.BuildAsync();

        await Assert.That(root.GetValue("Key")).IsEqualTo("");
    }
}
