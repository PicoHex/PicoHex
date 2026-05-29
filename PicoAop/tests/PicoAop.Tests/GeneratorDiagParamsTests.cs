namespace PicoAop.Tests;

public class GeneratorDiagParamsTests
{
    [Test]
    public async Task Category_IsPicoAop()
    {
        await Assert.That(InterceptorDiagParams.Category).IsEqualTo("PicoAop");
    }

    [Test]
    public async Task InterceptorTypeMismatch_HasId_PICO010()
    {
        var d = InterceptorDiagParams.InterceptorTypeMismatch;
        await Assert.That(d.Id).IsEqualTo("PICO010");
        await Assert.That(d.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Error);
    }

    [Test]
    public async Task FilterRequiresInterface_HasId_PICO011()
    {
        var d = InterceptorDiagParams.FilterRequiresInterface;
        await Assert.That(d.Id).IsEqualTo("PICO011");
    }

    [Test]
    public async Task ZeroInterceptorsMatched_HasId_PICO012()
    {
        var d = InterceptorDiagParams.ZeroInterceptorsMatched;
        await Assert.That(d.Id).IsEqualTo("PICO012");
        await Assert.That(d.DefaultSeverity).IsEqualTo(DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task ConflictingInterceptorDeclaration_HasId_PICO013()
    {
        var d = InterceptorDiagParams.ConflictingInterceptorDeclaration;
        await Assert.That(d.Id).IsEqualTo("PICO013");
    }

    [Test]
    public async Task AmbiguousInterceptBy_HasId_PICO014()
    {
        var d = InterceptorDiagParams.AmbiguousInterceptBy;
        await Assert.That(d.Id).IsEqualTo("PICO014");
    }
}
