namespace PicoAop.Tests;


public class GeneratorNamesTests
{
    [Test]
    public async Task InterceptBy_MethodName_IsCorrect()
    {
        await Assert.That(PicoAopNames.InterceptBy).IsEqualTo("InterceptBy");
    }

    [Test]
    public async Task Register_MethodNames_AreCorrect()
    {
        await Assert.That(PicoAopNames.Register).IsEqualTo("Register");
        await Assert.That(PicoAopNames.RegisterTransient).IsEqualTo("RegisterTransient");
        await Assert.That(PicoAopNames.RegisterScoped).IsEqualTo("RegisterScoped");
        await Assert.That(PicoAopNames.RegisterSingleton).IsEqualTo("RegisterSingleton");
    }

    [Test]
    public async Task TypeNames_AreCorrect()
    {
        await Assert.That(PicoAopNames.IInterceptorFull).IsEqualTo("PicoAop.Abs.IInterceptor");
        await Assert
            .That(PicoAopNames.InterceptorBaseFull)
            .IsEqualTo("PicoAop.Abs.InterceptorBase");
        await Assert.That(PicoAopNames.VoidResultFull).IsEqualTo("PicoAop.Abs.VoidResult");
        await Assert.That(PicoAopNames.IInvocationFull).IsEqualTo("PicoAop.Abs.IInvocation`1");
        await Assert.That(PicoAopNames.ISvcContainerFull).IsEqualTo("PicoDI.Abs.ISvcContainer");
        await Assert.That(PicoAopNames.ISvcScopeFull).IsEqualTo("PicoDI.Abs.ISvcScope");
        await Assert.That(PicoAopNames.SvcContainerFull).IsEqualTo("PicoDI.SvcContainer");
        await Assert
            .That(PicoAopNames.SvcContainerAutoConfigFull)
            .IsEqualTo("PicoDI.SvcContainerAutoConfiguration");
    }
}
