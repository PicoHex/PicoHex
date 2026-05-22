namespace PicoDI.Aop.Tests;

public class FilterEvaluationTests
{
    public sealed class LogInterceptor : InterceptorBase { }

    [Test]
    public async Task WhereNamespace_Filter_StoredInContainerState()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.AddInterceptor<LogInterceptor>().WhereNamespace("MyApp");

        await Assert.That(container.InterceptorFilters.Count).IsEqualTo(1);
        var filter = container.InterceptorFilters[0] as NamespaceFilter;
        await Assert.That(filter).IsNotNull();
        await Assert.That(filter!.InterceptorType).IsEqualTo(typeof(LogInterceptor));
    }

    [Test]
    public async Task NamespaceFilter_Matches_CorrectNamespace()
    {
        var filter = new NamespaceFilter(typeof(LogInterceptor), "PicoDI.Aop.Tests");
        await Assert.That(filter.Matches(typeof(FilterEvaluationTests))).IsTrue();
    }

    [Test]
    public async Task NamespaceFilter_Rejects_DifferentNamespace()
    {
        var filter = new NamespaceFilter(typeof(LogInterceptor), "MyApp");
        await Assert.That(filter.Matches(typeof(FilterEvaluationTests))).IsFalse();
    }
}
