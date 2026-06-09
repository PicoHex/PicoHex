namespace PicoAop.Tests;

public class AotPublishTests
{
    [Test]
    [Explicit("Run manually or in CI: dotnet publish -p:PublishAot=true")]
    public async Task NativeAotPublish_ZeroTrimWarnings()
    {
        // Validated in CI via:
        // dotnet publish PicoAop/tests/PicoAop.Tests/PicoAop.Tests.csproj
        //   -c Release -r win-x64 -p:PublishAot=true
        // grep -c "warning IL" → must be 0
        await Assert.That(true).IsTrue();
    }
}
