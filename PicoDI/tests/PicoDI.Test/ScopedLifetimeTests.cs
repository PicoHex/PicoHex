namespace PicoDI.Test;

/// <summary>
/// Tests for Scoped lifetime with all registration methods and resolution methods.
/// Scoped: A single instance is created and shared within a scope.
/// </summary>
public class ScopedLifetimeTests
{
    #region Factory Registration + GetService

    [Test]
    public async Task Factory_GetService_ReturnsSameInstanceWithinScope()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<ISimpleService>();
        var instance2 = scope.GetService<ISimpleService>();

        // Assert
        await Assert.That(instance1).IsNotNull();
        await Assert.That(instance2).IsNotNull();
        await Assert.That(instance1.InstanceId).IsEqualTo(instance2.InstanceId);
    }

    [Test]
    public async Task Factory_GetService_DifferentScopes_ReturnsDifferentInstances()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Act
        var instance1 = scope1.GetService<ISimpleService>();
        var instance2 = scope2.GetService<ISimpleService>();

        // Assert - Different scopes should have different instances
        await Assert.That(instance1.InstanceId).IsNotEqualTo(instance2.InstanceId);
    }

    [Test]
    public async Task FactoryWithServiceAndImpl_GetService_ReturnsSameInstanceWithinScope()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService, SimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<ISimpleService>();
        var instance2 = scope.GetService<ISimpleService>();

        // Assert
        await Assert.That(instance1.InstanceId).IsEqualTo(instance2.InstanceId);
    }

    [Test]
    public async Task FactoryWithType_GetService_ReturnsSameInstanceWithinScope()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<ISimpleService>();
        var instance2 = scope.GetService<ISimpleService>();

        // Assert
        await Assert.That(instance1.InstanceId).IsEqualTo(instance2.InstanceId);
    }

    #endregion

    #region Factory Registration + GetServices

    [Test]
    public async Task Factory_GetServices_ReturnsAllRegistrations_SameInstancesWithinScope()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<INotificationService>(static _ => new EmailNotificationService());
        container.RegisterScoped<INotificationService>(static _ => new SmsNotificationService());
        container.RegisterScoped<INotificationService>(static _ => new PushNotificationService());
        await using var scope = container.CreateScope();

        // Act
        var services1 = scope.GetServices<INotificationService>().ToList();
        var services2 = scope.GetServices<INotificationService>().ToList();

        // Assert
        await Assert.That(services1.Count).IsEqualTo(3);
        await Assert.That(services2.Count).IsEqualTo(3);

        // Same instances within scope
        for (int i = 0; i < services1.Count; i++)
        {
            await Assert.That(services1[i].InstanceId).IsEqualTo(services2[i].InstanceId);
        }
    }

    [Test]
    public async Task Factory_GetServices_DifferentScopes_DifferentInstances()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Act
        var services1 = scope1.GetServices<ISimpleService>().ToList();
        var services2 = scope2.GetServices<ISimpleService>().ToList();

        // Assert - Different scopes have different instances
        await Assert.That(services1[0].InstanceId).IsNotEqualTo(services2[0].InstanceId);
        await Assert.That(services1[1].InstanceId).IsNotEqualTo(services2[1].InstanceId);
    }

    #endregion

    #region Factory Registration + Non-Generic GetService

    [Test]
    public async Task Factory_NonGenericGetService_ReturnsSameInstanceWithinScope()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Act
        var instance1 = (ISimpleService)scope.GetService(typeof(ISimpleService));
        var instance2 = (ISimpleService)scope.GetService(typeof(ISimpleService));

        // Assert
        await Assert.That(instance1.InstanceId).IsEqualTo(instance2.InstanceId);
    }

    [Test]
    public async Task Factory_NonGenericGetServices_ReturnsSameInstancesWithinScope()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<INotificationService>(static _ => new EmailNotificationService());
        container.RegisterScoped<INotificationService>(static _ => new SmsNotificationService());
        await using var scope = container.CreateScope();

        // Act
        var services1 = scope
            .GetServices(typeof(INotificationService))
            .Cast<INotificationService>()
            .ToList();
        var services2 = scope
            .GetServices(typeof(INotificationService))
            .Cast<INotificationService>()
            .ToList();

        // Assert
        await Assert.That(services1.Count).IsEqualTo(2);
        await Assert.That(services1[0].InstanceId).IsEqualTo(services2[0].InstanceId);
        await Assert.That(services1[1].InstanceId).IsEqualTo(services2[1].InstanceId);
    }

    #endregion

    #region Dependency Injection Chain Tests

    [Test]
    public async Task Factory_ScopedWithScopedDependency_SameInstancesWithinScope()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        container.RegisterScoped<IServiceWithDependency>(
            static s => new ServiceWithDependency(s.GetService<ISimpleService>())
        );
        await using var scope = container.CreateScope();

        // Act
        var service1 = scope.GetService<IServiceWithDependency>();
        var service2 = scope.GetService<IServiceWithDependency>();
        var directDep = scope.GetService<ISimpleService>();

        // Assert - All same instances within scope
        await Assert.That(service1.InstanceId).IsEqualTo(service2.InstanceId);
        await Assert.That(service1.Dependency.InstanceId).IsEqualTo(directDep.InstanceId);
    }

    [Test]
    public async Task Factory_ScopedWithScopedDependency_DifferentInstancesAcrossScopes()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        container.RegisterScoped<IServiceWithDependency>(
            static s => new ServiceWithDependency(s.GetService<ISimpleService>())
        );
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Act
        var service1 = scope1.GetService<IServiceWithDependency>();
        var service2 = scope2.GetService<IServiceWithDependency>();

        // Assert - Different instances across scopes
        await Assert.That(service1.InstanceId).IsNotEqualTo(service2.InstanceId);
        await Assert
            .That(service1.Dependency.InstanceId)
            .IsNotEqualTo(service2.Dependency.InstanceId);
    }

    [Test]
    public async Task Factory_DeepDependencyChain_AllScoped()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ILevelOneService>(static _ => new LevelOneService());
        container.RegisterScoped<ILevelTwoService>(
            static s => new LevelTwoService(s.GetService<ILevelOneService>())
        );
        container.RegisterScoped<ILevelThreeService>(
            static s => new LevelThreeService(s.GetService<ILevelTwoService>())
        );
        await using var scope = container.CreateScope();

        // Act
        var service1 = scope.GetService<ILevelThreeService>();
        var service2 = scope.GetService<ILevelThreeService>();
        var levelTwo = scope.GetService<ILevelTwoService>();
        var levelOne = scope.GetService<ILevelOneService>();

        // Assert - Entire chain is same instance within scope
        await Assert.That(service1.InstanceId).IsEqualTo(service2.InstanceId);
        await Assert.That(service1.LevelTwo.InstanceId).IsEqualTo(levelTwo.InstanceId);
        await Assert.That(service1.LevelTwo.LevelOne.InstanceId).IsEqualTo(levelOne.InstanceId);
    }

    #endregion

    #region Child Scope Tests

    [Test]
    public async Task Factory_ChildScope_ScopedHasDifferentInstance()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        await using var parentScope = container.CreateScope();
        await using var childScope = parentScope.CreateScope();

        // Act
        var parentInstance1 = parentScope.GetService<ISimpleService>();
        var parentInstance2 = parentScope.GetService<ISimpleService>();
        var childInstance1 = childScope.GetService<ISimpleService>();
        var childInstance2 = childScope.GetService<ISimpleService>();

        // Assert - Parent scope same, child scope same, but different between scopes
        await Assert.That(parentInstance1.InstanceId).IsEqualTo(parentInstance2.InstanceId);
        await Assert.That(childInstance1.InstanceId).IsEqualTo(childInstance2.InstanceId);
        await Assert.That(parentInstance1.InstanceId).IsNotEqualTo(childInstance1.InstanceId);
    }

    [Test]
    public async Task Factory_NestedChildScopes_EachHasOwnInstance()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        await using var rootScope = container.CreateScope();
        await using var childScope = rootScope.CreateScope();
        await using var grandchildScope = childScope.CreateScope();

        // Act
        var rootInstance = rootScope.GetService<ISimpleService>();
        var childInstance = childScope.GetService<ISimpleService>();
        var grandchildInstance = grandchildScope.GetService<ISimpleService>();

        // Assert - Each scope level has its own instance
        await Assert.That(rootInstance.InstanceId).IsNotEqualTo(childInstance.InstanceId);
        await Assert.That(childInstance.InstanceId).IsNotEqualTo(grandchildInstance.InstanceId);
        await Assert.That(rootInstance.InstanceId).IsNotEqualTo(grandchildInstance.InstanceId);
    }

    #endregion

    #region Generic Register with Lifetime Parameter

    [Test]
    public async Task Register_WithLifetimeParameter_Scoped_SameInstanceWithinScope()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.Register<ISimpleService>(static _ => new SimpleService(), SvcLifetime.Scoped);
        await using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<ISimpleService>();
        var instance2 = scope.GetService<ISimpleService>();

        // Assert
        await Assert.That(instance1.InstanceId).IsEqualTo(instance2.InstanceId);
    }

    #endregion

    #region Disposal Tests

    [Test]
    public async Task ScopedDisposable_DisposedWhenScopeDisposed()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IDisposableService>(static _ => new DisposableService());
        DisposableService capturedInstance;

        // Act
        await using (var scope = container.CreateScope())
        {
            capturedInstance = (DisposableService)scope.GetService<IDisposableService>();
            await Assert.That(capturedInstance.IsDisposed).IsFalse();
        }

        // Assert
        await Assert.That(capturedInstance.IsDisposed).IsTrue();
    }

    [Test]
    public async Task ScopedAsyncDisposable_DisposedWhenScopeDisposedAsync()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<IAsyncDisposableService>(static _ => new AsyncDisposableService());
        AsyncDisposableService capturedInstance;

        // Act
        await using (var scope = container.CreateScope())
        {
            capturedInstance = (AsyncDisposableService)scope.GetService<IAsyncDisposableService>();
            await Assert.That(capturedInstance.IsDisposed).IsFalse();
        }

        // Assert
        await Assert.That(capturedInstance.IsDisposed).IsTrue();
    }

    #endregion
}
