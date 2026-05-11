namespace PicoCfg.Tests;

public class CfgTests
{
    [Test]
    public async Task CreateBuilder_ReturnsNonNull()
    {
        var builder = Cfg.CreateBuilder();
        await Assert.That(builder).IsNotNull();
    }
}
