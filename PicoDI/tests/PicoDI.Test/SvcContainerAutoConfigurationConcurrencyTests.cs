namespace PicoDI.Test;

/// <summary>
/// Concurrency semantics for <see cref="SvcContainerAutoConfiguration"/>.
///
/// Regression: configurators used to run UNDER the static apply lock. A
/// configurator that triggered application on ANOTHER container from a
/// different thread (e.g. the first JIT of a cold assembly runs a module
/// initializer that constructs a container) deadlocked — the JIT thread held
/// the apply lock while the initializer thread waited for it. The fix mirrors
/// <c>MediatorAutoSubscriptionRegistry</c>: bookkeeping under the lock,
/// configurators run OUTSIDE it.
/// </summary>
[NotInParallel]
public class SvcContainerAutoConfigurationConcurrencyTests
{
    [Test]
    public async Task ConfiguratorRunsOutsideApplyLock_CrossThreadApplyOnOtherContainerDoesNotDeadlock()
    {
        SvcContainerAutoConfiguration.ClearForTesting();
        try
        {
            await using var outer = new SvcContainer(autoConfigureFromGenerator: false);
            await using var inner = new SvcContainer(autoConfigureFromGenerator: false);

            var innerConfigured = 0;

            SvcContainerAutoConfiguration.RegisterConfigurator(
                "concurrency::outer",
                _ =>
                {
                    // Simulate a module initializer running on another thread
                    // while this configurator executes: apply configuration to
                    // a DIFFERENT container. Under the old implementation the
                    // apply lock was held for the whole configurator run, so
                    // the inner thread blocked forever and this wait timed out.
                    var innerApply = Task.Run(() =>
                        SvcContainerAutoConfiguration.TryApplyConfiguration(inner)
                    );
                    var completed = innerApply.Wait(TimeSpan.FromSeconds(5));
                    if (!completed)
                        throw new TimeoutException(
                            "Applying configuration to another container deadlocked: "
                                + "the apply lock is held while configurators run."
                        );
                }
            );
            SvcContainerAutoConfiguration.RegisterConfigurator(
                "concurrency::inner",
                _ => innerConfigured++
            );

            SvcContainerAutoConfiguration.TryApplyConfiguration(outer);

            // The global registry applies every configurator to every
            // container: "concurrency::inner" runs once for the outer
            // container and once for the inner container. The assertion
            // proves the cross-thread inner apply completed (no deadlock).
            await Assert.That(innerConfigured).IsEqualTo(2);
            await Assert
                .That(SvcContainerAutoConfiguration.HasAppliedGeneratedConfiguration(inner))
                .IsTrue();
        }
        finally
        {
            SvcContainerAutoConfiguration.ClearForTesting();
        }
    }

    [Test]
    public async Task ConcurrentTryApply_SameContainer_ConfiguratorsRunExactlyOnce()
    {
        SvcContainerAutoConfiguration.ClearForTesting();
        try
        {
            await using var container = new SvcContainer(autoConfigureFromGenerator: false);

            var runCount = 0;
            SvcContainerAutoConfiguration.RegisterConfigurator(
                "concurrency::exactly-once",
                _ => Interlocked.Increment(ref runCount)
            );

            var attempts = Enumerable
                .Range(0, 8)
                .Select(_ =>
                    Task.Run(() => SvcContainerAutoConfiguration.TryApplyConfiguration(container))
                )
                .ToArray();

            await Task.WhenAll(attempts);

            await Assert.That(Volatile.Read(ref runCount)).IsEqualTo(1);
            await Assert
                .That(SvcContainerAutoConfiguration.HasAppliedGeneratedConfiguration(container))
                .IsTrue();
        }
        finally
        {
            SvcContainerAutoConfiguration.ClearForTesting();
        }
    }

    [Test]
    public async Task ThrowingConfigurator_NotMarkedApplied_RetryAppliesAgain()
    {
        SvcContainerAutoConfiguration.ClearForTesting();
        try
        {
            await using var container = new SvcContainer(autoConfigureFromGenerator: false);

            var attempts = 0;
            SvcContainerAutoConfiguration.RegisterConfigurator(
                "concurrency::throw-once",
                _ =>
                {
                    if (Interlocked.Increment(ref attempts) == 1)
                        throw new InvalidOperationException("first run fails");
                }
            );

            await Assert
                .That(() => SvcContainerAutoConfiguration.TryApplyConfiguration(container))
                .Throws<InvalidOperationException>();

            await Assert
                .That(SvcContainerAutoConfiguration.HasAppliedGeneratedConfiguration(container))
                .IsFalse();

            // The failed run must not mark the container — a retry re-runs the
            // configurator and succeeds.
            var retry = SvcContainerAutoConfiguration.TryApplyConfiguration(container);
            await Assert.That(retry).IsTrue();
            await Assert.That(Volatile.Read(ref attempts)).IsEqualTo(2);
            await Assert
                .That(SvcContainerAutoConfiguration.HasAppliedGeneratedConfiguration(container))
                .IsTrue();
        }
        finally
        {
            SvcContainerAutoConfiguration.ClearForTesting();
        }
    }
}
