namespace PicoAop.Tests;


public class DiExtensionTests
{
    public sealed class TestInterceptor : InterceptorBase { }

    [Test]
    public async Task InterceptBy_ReturnsSameContainer()
    {
        var container = new SvcContainer();
        var result = container.InterceptBy<TestInterceptor>();
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task WithoutInterceptors_ReturnsSameContainer()
    {
        var container = new SvcContainer();
        var result = container.WithoutInterceptors();
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task WithoutInterceptor_ReturnsSameContainer()
    {
        var container = new SvcContainer();
        var result = container.WithoutInterceptor<TestInterceptor>();
        await Assert.That(result).IsSameReferenceAs(container);
    }

    [Test]
    public async Task AddInterceptor_ReturnsSameContainer()
    {
        var container = new SvcContainer();
        var result = container.AddInterceptor<TestInterceptor>();
        await Assert.That(result).IsSameReferenceAs(container);
    }
}
