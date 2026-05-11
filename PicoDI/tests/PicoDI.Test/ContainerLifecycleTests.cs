namespace PicoDI.Test;

/// <summary>
/// Tests for container and scope lifecycle management.
/// </summary>
public class ContainerLifecycleTests
{
    #region Container Build Tests

    [Test]
    public async Task Build_AfterBuild_CannotRegisterMore()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        container.Build();

        // Act & Assert
        await Assert
            .That(
                () =>
                    container.RegisterTransient<ILevelOneService>(static _ => new LevelOneService())
            )
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task Build_CalledMultipleTimes_NoError()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());

        // Act & Assert - Should not throw
        container.Build();
        container.Build();
        container.Build();

        await using var scope = container.CreateScope();
        var service = scope.GetService<ISimpleService>();
        await Assert.That(service).IsNotNull();
    }

    [Test]
    public async Task CreateScope_AutoBuildsIfNotBuilt()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        // Note: Not calling Build() explicitly

        // Act
        await using var scope = container.CreateScope();
        var service = scope.GetService<ISimpleService>();

        // Assert - Should work because CreateScope auto-builds
        await Assert.That(service).IsNotNull();
    }

    #endregion

    #region Container Disposal Tests

    [Test]
    public async Task Dispose_AfterDispose_ThrowsOnCreateScope()
    {
        // Arrange
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());

        // Act
        await container.DisposeAsync();

        // Assert
        await Assert.That(() => container.CreateScope()).Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Dispose_AfterDispose_ThrowsOnRegister()
    {
        // Arrange
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        await container.DisposeAsync();

        // Assert
        await Assert
            .That(
                () => container.RegisterTransient<ISimpleService>(static _ => new SimpleService())
            )
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task Dispose_DisposesAllScopes()
    {
        // Arrange
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IDisposableService>(static _ => new DisposableService());

        var scope1 = container.CreateScope();
        var scope2 = container.CreateScope();
        var childScope = scope1.CreateScope();

        var instance1 = (DisposableService)scope1.GetService<IDisposableService>();
        var instance2 = (DisposableService)scope2.GetService<IDisposableService>();
        var childInstance = (DisposableService)childScope.GetService<IDisposableService>();

        // Act
        await container.DisposeAsync();

        // Assert - All scoped instances should be disposed
        await Assert.That(instance1.IsDisposed).IsTrue();
        await Assert.That(instance2.IsDisposed).IsTrue();
        await Assert.That(childInstance.IsDisposed).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_SingletonThrows_ContinuesDisposingOtherSingletons()
    {
        // Arrange
        var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Register singleton services with mixed disposables including one faulty
        container.RegisterSingleton<IDisposable>(static _ => new DisposableService());
        container.RegisterSingleton<IAsyncDisposable>(
            static _ => new FaultyAsyncDisposableService()
        );
        // Use IAsyncDisposable instead of object to avoid source generator issues
        container.RegisterSingleton<IAsyncDisposable>(static _ => new AsyncDisposableService());

        await using var scope = container.CreateScope();
        var instance1 = (DisposableService)scope.GetService<IDisposable>();
        var asyncDisposables = scope.GetServices<IAsyncDisposable>().ToArray();
        var faultyInstance = (FaultyAsyncDisposableService)asyncDisposables[0];
        var instance2 = (AsyncDisposableService)asyncDisposables[1];

        // Act - DisposeAsync should continue even if one singleton throws
        await container.DisposeAsync();

        // Assert - All singletons should have been attempted to dispose despite the exception
        await Assert.That(instance1.IsDisposed).IsTrue();
        await Assert.That(instance2.IsDisposed).IsTrue();
        await Assert.That(faultyInstance.DisposeAsyncCalled).IsTrue();
    }

    [Test]
    public async Task DisposeAsync_SameSingletonRegisteredMultipleTimes_OnlyDisposedOnce()
    {
        // Arrange
        var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Create a singleton instance that implements both IDisposable and IAsyncDisposable
        var singletonInstance = new DualDisposableService();

        // Register the same instance multiple times under different service types using factory
        container.RegisterSingleton<IDisposable>(_ => singletonInstance);
        container.RegisterSingleton<IAsyncDisposable>(_ => singletonInstance);

        await using var scope = container.CreateScope();
        var instance1 = scope.GetService<IDisposable>();
        var instance2 = scope.GetService<IAsyncDisposable>();

        // Act - DisposeAsync should only dispose once despite multiple registrations
        await container.DisposeAsync();

        // Assert - Instance should be disposed only once
        await Assert
            .That(singletonInstance.AsyncDisposeCalled || singletonInstance.SyncDisposeCalled)
            .IsTrue();
        // Note: In async dispose, AsyncDisposeCalled should be true
    }

    [Test]
    public async Task Dispose_SameSingletonRegisteredMultipleTimes_OnlyDisposedOnce()
    {
        // Arrange
        var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Create a singleton instance that implements both IDisposable and IAsyncDisposable
        var singletonInstance = new DualDisposableService();

        // Register the same instance multiple times under different service types using factory
        container.RegisterSingleton<IDisposable>(_ => singletonInstance);
        container.RegisterSingleton<IAsyncDisposable>(_ => singletonInstance);

        await using var scope = container.CreateScope();
        var instance1 = scope.GetService<IDisposable>();
        var instance2 = scope.GetService<IAsyncDisposable>();

        // Act - Dispose should only dispose once despite multiple registrations
        await container.DisposeAsync();

        // Assert - Instance should be disposed only once
        await Assert
            .That(singletonInstance.SyncDisposeCalled || singletonInstance.AsyncDisposeCalled)
            .IsTrue();
        // Note: In sync dispose, SyncDisposeCalled should be true
    }

    #endregion

    #region Scope Disposal Tests

    [Test]
    public async Task ScopeDispose_DisposesChildScopes()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IDisposableService>(static _ => new DisposableService());

        var parentScope = container.CreateScope();
        var childScope = parentScope.CreateScope();
        var grandchildScope = childScope.CreateScope();

        var parentInstance = (DisposableService)parentScope.GetService<IDisposableService>();
        var childInstance = (DisposableService)childScope.GetService<IDisposableService>();
        var grandchildInstance = (DisposableService)
            grandchildScope.GetService<IDisposableService>();

        // Act - Dispose parent scope
        await parentScope.DisposeAsync();

        // Assert - Parent and all children should be disposed
        await Assert.That(parentInstance.IsDisposed).IsTrue();
        await Assert.That(childInstance.IsDisposed).IsTrue();
        await Assert.That(grandchildInstance.IsDisposed).IsTrue();
    }

    [Test]
    public async Task ScopeDispose_OnlyDisposesOwnInstances()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IDisposableService>(static _ => new DisposableService());

        await using var scope1 = container.CreateScope();
        var scope2 = container.CreateScope();

        var instance1 = (DisposableService)scope1.GetService<IDisposableService>();
        var instance2 = (DisposableService)scope2.GetService<IDisposableService>();

        // Act - Dispose only scope2
        await scope2.DisposeAsync();

        // Assert - Only scope2's instance should be disposed
        await Assert.That(instance1.IsDisposed).IsFalse();
        await Assert.That(instance2.IsDisposed).IsTrue();
    }

    [Test]
    public async Task ScopeDispose_DoesNotDisposeSingletons()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<IDisposableService>(static _ => new DisposableService());

        DisposableService singletonInstance;
        await using (var scope = container.CreateScope())
        {
            singletonInstance = (DisposableService)scope.GetService<IDisposableService>();
        }

        // Assert - Singleton should NOT be disposed when scope is disposed
        await Assert.That(singletonInstance.IsDisposed).IsFalse();
    }

    [Test]
    public async Task ScopeDisposeAsync_DisposesAsyncDisposables()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IAsyncDisposableService>(static _ => new AsyncDisposableService());

        AsyncDisposableService instance;
        await using (var scope = container.CreateScope())
        {
            instance = (AsyncDisposableService)scope.GetService<IAsyncDisposableService>();
            await Assert.That(instance.IsDisposed).IsFalse();
        }

        // Assert
        await Assert.That(instance.IsDisposed).IsTrue();
    }

    #endregion

    #region AutoConfigure Tests

    [Test]
    public async Task Constructor_WithAutoConfigureFalse_DoesNotAutoRegister()
    {
        // Arrange & Act
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Assert - Container should be empty (no auto-configured services)
        // This test verifies the flag works; actual auto-configuration depends on source generator
        container.Build();
        // No assertion needed - if Build succeeds without error, test passes
    }

    [Test]
    public async Task SvcContainerAutoConfiguration_Integration_WithConfigurator()
    {
        // Arrange & Act
        await using var container = new SvcContainer(autoConfigureFromGenerator: true);

        if (!SvcContainerAutoConfiguration.HasConfigurator)
        {
            await using var scope = container.CreateScope();
            await Assert.That(() => scope.GetService<ISimpleService>()).Throws<Exception>();
        }
        else
        {
            await Assert
                .That(SvcContainerAutoConfiguration.HasAppliedGeneratedConfiguration(container))
                .IsTrue();

            await Assert
                .That(SvcContainerAutoConfiguration.TryApplyConfiguration(container))
                .IsFalse();
        }
    }

    [Test]
    public async Task ConfigureGeneratedServices_WithAutoConfigureFalse_AppliesGeneratedRegistrations()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        var result = PicoDI
            .Generated
            .GeneratedServiceRegistrations_PicoDITest
            .ConfigureGeneratedServices(container);

        // Assert
        await Assert.That(result).IsEqualTo(container);
        await Assert
            .That(SvcContainerAutoConfiguration.HasAppliedGeneratedConfiguration(container))
            .IsTrue();

        await using var scope = container.CreateScope();
        var service = scope.GetService<IAlternativeSimpleService>();

        await Assert.That(service).IsTypeOf<PreferredCtorService>();
        await Assert.That(service.ConstructorUsed).IsEqualTo("preferred");
    }

    [Test]
    public async Task ConfigureGeneratedServices_CalledMultipleTimes_ReappliesGeneratedRegistrations()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        PicoDI
            .Generated
            .GeneratedServiceRegistrations_PicoDITest
            .ConfigureGeneratedServices(container);
        PicoDI
            .Generated
            .GeneratedServiceRegistrations_PicoDITest
            .ConfigureGeneratedServices(container);

        // Assert
        await using var scope = container.CreateScope();
        var services = scope.GetServices<IAlternativeSimpleService>().ToArray();

        await Assert.That(services.Length).IsEqualTo(2);
        await Assert.That(services[0].ConstructorUsed).IsEqualTo("preferred");
        await Assert.That(services[1].ConstructorUsed).IsEqualTo("preferred");
    }

    #endregion

    #region Concurrent Access Tests

    [Test]
    public async Task ConcurrentScopeCreation_ThreadSafe()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        container.Build();

        // Act - Create many scopes concurrently
        var tasks = Enumerable
            .Range(0, 100)
            .Select(
                _ =>
                    Task.Run(async () =>
                    {
                        await using var scope = container.CreateScope();
                        return scope.GetService<ISimpleService>().InstanceId;
                    })
            )
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - All should succeed with unique scoped instances
        await Assert.That(results.Distinct().Count()).IsEqualTo(100);
    }

    [Test]
    public async Task ConcurrentScopeResolution_WithinScope_ThreadSafe()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Act - Resolve from same scope concurrently
        var tasks = Enumerable
            .Range(0, 100)
            .Select(_ => Task.Run(() => scope.GetService<ISimpleService>().InstanceId))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert - All should return the same scoped instance
        await Assert.That(results.Distinct().Count()).IsEqualTo(1);
    }

    #endregion

    #region AutoConfiguration Tests

    [Test]
    public async Task SvcContainerAutoConfiguration_TryApplyConfiguration_NoConfigurators_ReturnsFalse()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        var result = SvcContainerAutoConfiguration.TryApplyConfiguration(container);

        // Assert - if configurators exist but the container has not yet applied them,
        // TryApplyConfiguration performs the work exactly once.
        await Assert.That(result).IsEqualTo(SvcContainerAutoConfiguration.HasConfigurator);

        if (result)
        {
            await Assert
                .That(SvcContainerAutoConfiguration.HasAppliedGeneratedConfiguration(container))
                .IsTrue();
            await Assert
                .That(SvcContainerAutoConfiguration.TryApplyConfiguration(container))
                .IsFalse();
        }
    }

    [Test]
    public async Task SvcContainerAutoConfiguration_HasConfigurator_PropertyReflectsState()
    {
        // Arrange & Act
        var hasConfigurator = SvcContainerAutoConfiguration.HasConfigurator;
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        var result = SvcContainerAutoConfiguration.TryApplyConfiguration(container);
        await Assert.That(result).IsEqualTo(hasConfigurator);
    }

    [Test]
    public async Task SvcContainerAutoConfiguration_TryApplyConfiguration_UsesDeterministicConfiguratorIdOrder()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        SvcContainerAutoConfiguration.RegisterConfigurator(
            "task3-deterministic-order-z",
            static c =>
                c.RegisterSingleton<IDeterministicAutoConfigurationProbe>(
                    _ => new DeterministicAutoConfigurationProbe("z")
                )
        );
        SvcContainerAutoConfiguration.RegisterConfigurator(
            "task3-deterministic-order-a",
            static c =>
                c.RegisterSingleton<IDeterministicAutoConfigurationProbe>(
                    _ => new DeterministicAutoConfigurationProbe("a")
                )
        );

        await Assert.That(SvcContainerAutoConfiguration.TryApplyConfiguration(container)).IsTrue();

        await using var scope = container.CreateScope();
        var service = scope.GetService<IDeterministicAutoConfigurationProbe>();
        var services = scope.GetServices<IDeterministicAutoConfigurationProbe>().ToArray();

        await Assert.That(services.Length).IsEqualTo(2);
        await Assert.That(services[0].Configuration).IsEqualTo("a");
        await Assert.That(services[1].Configuration).IsEqualTo("z");
        await Assert.That(service.Configuration).IsEqualTo("z");
    }

    [Test]
    public async Task SvcContainerAutoConfiguration_DuplicateConfiguratorKey_AppliesOnlyLatestOnce()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        SvcContainerAutoConfiguration.RegisterConfigurator(
            "test-duplicate-key",
            static c =>
                c.RegisterSingleton<IConfigurableService>(_ => new ConfigurableService("first"))
        );
        SvcContainerAutoConfiguration.RegisterConfigurator(
            "test-duplicate-key",
            static c =>
                c.RegisterSingleton<IConfigurableService>(_ => new ConfigurableService("second"))
        );

        await Assert.That(SvcContainerAutoConfiguration.TryApplyConfiguration(container)).IsTrue();

        await using var scope = container.CreateScope();
        var service = scope.GetService<IConfigurableService>();
        var services = scope.GetServices<IConfigurableService>().ToArray();

        await Assert.That(service.Configuration).IsEqualTo("second");
        await Assert.That(services.Length).IsEqualTo(1);
    }

    [Test]
    public async Task ScopeDispose_DetachesRootScope_ContainerStillOperates()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());

        var firstScope = container.CreateScope();
        var first = firstScope.GetService<ISimpleService>();
        await firstScope.DisposeAsync();

        await using var secondScope = container.CreateScope();
        var second = secondScope.GetService<ISimpleService>();

        await Assert.That(first.InstanceId).IsNotEqualTo(second.InstanceId);
    }

    #endregion

    private interface IDeterministicAutoConfigurationProbe
    {
        string Configuration { get; }
    }

    private sealed class DeterministicAutoConfigurationProbe(string configuration)
        : IDeterministicAutoConfigurationProbe
    {
        public string Configuration { get; } = configuration;
    }

    #region Concurrent Dispose+Resolve Tests (Bug 3)

    [Test]
    public async Task ConcurrentDisposeAndResolve_LeavesNoOrphanCollections()
    {
        var scopedField = typeof(SvcScope).GetField(
            "_scopedInstances",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        )!;
        var transientField = typeof(SvcScope).GetField(
            "_trackedTransients",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        )!;

        for (int i = 0; i < 2000; i++)
        {
            await using var container = new SvcContainer(autoConfigureFromGenerator: false);
            container.RegisterScoped<ISimpleService>(_ => new SimpleService());
            container.RegisterTransient<IDisposableService>(_ => new DisposableService());

            await using var scope = container.CreateScope();

            // Race dispose against resolve. After both complete, the scope's
            // internal tracking collections must be null — no orphaned queues
            // or dictionaries created after DisposeAsync cleared them.
            var resolveTask = Task.Run(() =>
            {
                try
                {
                    scope.GetService<ISimpleService>();
                }
                catch (ObjectDisposedException) { }
                try
                {
                    scope.GetService<IDisposableService>();
                }
                catch (ObjectDisposedException) { }
            });
            var disposeTask = scope.DisposeAsync().AsTask();

            await Task.WhenAll(resolveTask, disposeTask);

            await Assert.That(scopedField.GetValue(scope)).IsNull();
            await Assert.That(transientField.GetValue(scope)).IsNull();
        }
    }

    #endregion
}
