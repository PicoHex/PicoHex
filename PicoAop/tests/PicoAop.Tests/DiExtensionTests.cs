namespace PicoAop.Tests;

public class DiExtensionTests
{
    public sealed class TestInterceptor : InterceptorBase { }

    [Test]
    public async Task InterceptBy_ThrowsWithoutGenerator()
    {
        var container = new SvcContainer();
        await Assert.ThrowsAsync(() =>
        {
            container.InterceptBy<TestInterceptor>();
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task WithoutInterceptors_ThrowsWithoutGenerator()
    {
        var container = new SvcContainer();
        await Assert.ThrowsAsync(() =>
        {
            container.WithoutInterceptors();
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task WithoutInterceptor_ThrowsWithoutGenerator()
    {
        var container = new SvcContainer();
        await Assert.ThrowsAsync(() =>
        {
            container.WithoutInterceptor<TestInterceptor>();
            return Task.CompletedTask;
        });
    }

    [Test]
    public async Task AddInterceptor_ThrowsWithoutGenerator()
    {
        var container = new SvcContainer();
        await Assert.ThrowsAsync(() =>
        {
            container.AddInterceptor<TestInterceptor>();
            return Task.CompletedTask;
        });
    }
}
