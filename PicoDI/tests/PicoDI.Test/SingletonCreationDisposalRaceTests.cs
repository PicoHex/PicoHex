namespace PicoDI.Test;

/// <summary>
/// Regression tests for the race between concurrent scope disposal and
/// in-flight singleton creation.
///
/// Bug: <c>GetOrCreateSingletonSlow</c> read <c>OwningContainer!</c> AFTER the
/// factory returned. Scope disposal detaches the scope and nulls
/// <c>OwningContainer</c> (tracking-list bookkeeping), so a scope disposed
/// while the factory was running produced a NullReferenceException at
/// <c>OwningContainer!.NextSingletonCreationOrder()</c> instead of a valid
/// outcome.
/// </summary>
public class SingletonCreationDisposalRaceTests
{
    private sealed class SingletonSvc;

    [Test]
    public async Task SingletonCreation_ScopeDisposedDuringFactory_NeverThrowsNRE()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);

        using var factoryEntered = new ManualResetEventSlim(false);
        using var releaseFactory = new ManualResetEventSlim(false);

        container.RegisterSingleton<SingletonSvc>(_ =>
        {
            factoryEntered.Set();
            // Block until the test has disposed the scope, reproducing the race.
            releaseFactory.Wait(TimeSpan.FromSeconds(10));
            return new SingletonSvc();
        });
        container.Build();

        var scope = container.CreateScope();

        var resolution = Task.Run(() => scope.GetService<SingletonSvc>());

        // Ensure the factory is inside before disposing the scope.
        await Assert.That(factoryEntered.Wait(TimeSpan.FromSeconds(10))).IsTrue();

        await scope.DisposeAsync();

        // Let the factory finish. The container owns the singleton, so the
        // resolution may legitimately return the instance; it must never
        // crash with a NullReferenceException from the detached scope.
        releaseFactory.Set();

        Exception? caught = null;
        object? result = null;
        try
        {
            result = await resolution;
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        if (caught is not null)
        {
            await Assert.That(caught).IsTypeOf<ObjectDisposedException>();
        }
        else
        {
            await Assert.That(result).IsTypeOf<SingletonSvc>();
        }
    }

    [Test]
    public async Task SingletonCreation_ChildScopeDisposedDuringFactory_NeverThrowsNRE()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);

        using var factoryEntered = new ManualResetEventSlim(false);
        using var releaseFactory = new ManualResetEventSlim(false);

        container.RegisterSingleton<SingletonSvc>(_ =>
        {
            factoryEntered.Set();
            releaseFactory.Wait(TimeSpan.FromSeconds(10));
            return new SingletonSvc();
        });
        container.Build();

        var rootScope = container.CreateScope();
        var childScope = rootScope.CreateScope();

        var resolution = Task.Run(() => childScope.GetService<SingletonSvc>());

        await Assert.That(factoryEntered.Wait(TimeSpan.FromSeconds(10))).IsTrue();

        await childScope.DisposeAsync();
        releaseFactory.Set();

        Exception? caught = null;
        object? result = null;
        try
        {
            result = await resolution;
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        if (caught is not null)
        {
            await Assert.That(caught).IsTypeOf<ObjectDisposedException>();
        }
        else
        {
            await Assert.That(result).IsTypeOf<SingletonSvc>();
        }

        await rootScope.DisposeAsync();
    }
}
