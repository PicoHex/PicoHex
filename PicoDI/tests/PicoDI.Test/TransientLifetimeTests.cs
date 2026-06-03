namespace PicoDI.Test;

/// <summary>
/// Tests for Transient lifetime with all registration methods and resolution methods.
/// Transient: A new instance is created every time the service is requested.
/// </summary>
public class TransientLifetimeTests
{
    #region Factory Registration + GetService

    [Test]
    public async Task Factory_GetService_ReturnsNewInstanceEachTime()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<ISimpleService>();
        var instance2 = scope.GetService<ISimpleService>();

        // Assert
        await Assert.That(instance1).IsNotNull();
        await Assert.That(instance2).IsNotNull();
        await Assert.That(instance1.InstanceId).IsNotEqualTo(instance2.InstanceId);
    }

    [Test]
    public async Task Factory_GetService_DifferentScopes_ReturnsDifferentInstances()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Act
        var instance1 = scope1.GetService<ISimpleService>();
        var instance2 = scope2.GetService<ISimpleService>();

        // Assert
        await Assert.That(instance1.InstanceId).IsNotEqualTo(instance2.InstanceId);
    }

    [Test]
    public async Task FactoryWithServiceAndImpl_GetService_ReturnsNewInstanceEachTime()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService, SimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<ISimpleService>();
        var instance2 = scope.GetService<ISimpleService>();

        // Assert
        await Assert.That(instance1.InstanceId).IsNotEqualTo(instance2.InstanceId);
    }

    [Test]
    public async Task FactoryWithType_GetService_ReturnsNewInstanceEachTime()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<ISimpleService>();
        var instance2 = scope.GetService<ISimpleService>();

        // Assert
        await Assert.That(instance1.InstanceId).IsNotEqualTo(instance2.InstanceId);
    }

    #endregion

    #region Factory Registration + GetServices

    [Test]
    public async Task Factory_GetServices_ReturnsAllRegistrations()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<INotificationService>(
            static _ => new EmailNotificationService()
        );
        container.RegisterTransient<INotificationService>(static _ => new SmsNotificationService());
        container.RegisterTransient<INotificationService>(
            static _ => new PushNotificationService()
        );
        await using var scope = container.CreateScope();

        // Act
        var services = scope.GetServices<INotificationService>().ToList();

        // Assert
        await Assert.That(services.Count).IsEqualTo(3);
        var notificationTypes = services.Select(s => s.NotificationType).ToArray();
        await Assert.That(notificationTypes.Length).IsEqualTo(3);
        await Assert.That(notificationTypes[0]).IsEqualTo("Email");
        await Assert.That(notificationTypes[1]).IsEqualTo("SMS");
        await Assert.That(notificationTypes[2]).IsEqualTo("Push");
    }

    [Test]
    public async Task Factory_GetServices_EachCallReturnsNewInstances()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Act
        var services1 = scope.GetServices<ISimpleService>().ToList();
        var services2 = scope.GetServices<ISimpleService>().ToList();

        // Assert
        await Assert.That(services1.Count).IsEqualTo(2);
        await Assert.That(services2.Count).IsEqualTo(2);

        // All instances should be different (transient)
        var allIds = services1.Concat(services2).Select(s => s.InstanceId).ToList();
        await Assert.That(allIds.Distinct().Count()).IsEqualTo(4);
    }

    #endregion

    #region Factory Registration + Non-Generic GetService

    [Test]
    public async Task Factory_NonGenericGetService_ReturnsNewInstanceEachTime()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Act
        var instance1 = (ISimpleService)scope.GetService(typeof(ISimpleService));
        var instance2 = (ISimpleService)scope.GetService(typeof(ISimpleService));

        // Assert
        await Assert.That(instance1.InstanceId).IsNotEqualTo(instance2.InstanceId);
    }

    [Test]
    public async Task Factory_NonGenericGetServices_ReturnsAllRegistrations()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<INotificationService>(
            static _ => new EmailNotificationService()
        );
        container.RegisterTransient<INotificationService>(static _ => new SmsNotificationService());
        await using var scope = container.CreateScope();

        // Act
        var services = scope
            .GetServices(typeof(INotificationService))
            .Cast<INotificationService>()
            .ToList();

        // Assert
        await Assert.That(services.Count).IsEqualTo(2);
    }

    #endregion

    #region Dependency Injection Chain Tests

    [Test]
    public async Task Factory_TransientWithTransientDependency_BothNewEachTime()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        container.RegisterTransient<IServiceWithDependency>(static s => new ServiceWithDependency(
            s.GetService<ISimpleService>()
        ));
        await using var scope = container.CreateScope();

        // Act
        var service1 = scope.GetService<IServiceWithDependency>();
        var service2 = scope.GetService<IServiceWithDependency>();

        // Assert - Both service and dependency are new instances
        await Assert.That(service1.InstanceId).IsNotEqualTo(service2.InstanceId);
        await Assert
            .That(service1.Dependency.InstanceId)
            .IsNotEqualTo(service2.Dependency.InstanceId);
    }

    [Test]
    public async Task Factory_DeepDependencyChain_AllTransient()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ILevelOneService>(static _ => new LevelOneService());
        container.RegisterTransient<ILevelTwoService>(static s => new LevelTwoService(
            s.GetService<ILevelOneService>()
        ));
        container.RegisterTransient<ILevelThreeService>(static s => new LevelThreeService(
            s.GetService<ILevelTwoService>()
        ));
        await using var scope = container.CreateScope();

        // Act
        var service1 = scope.GetService<ILevelThreeService>();
        var service2 = scope.GetService<ILevelThreeService>();

        // Assert - Entire chain is new
        await Assert.That(service1.InstanceId).IsNotEqualTo(service2.InstanceId);
        await Assert.That(service1.LevelTwo.InstanceId).IsNotEqualTo(service2.LevelTwo.InstanceId);
        await Assert
            .That(service1.LevelTwo.LevelOne.InstanceId)
            .IsNotEqualTo(service2.LevelTwo.LevelOne.InstanceId);
    }

    #endregion

    #region Child Scope Tests

    [Test]
    public async Task Factory_ChildScope_TransientReturnsNewInstances()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        await using var parentScope = container.CreateScope();
        await using var childScope = parentScope.CreateScope();

        // Act
        var parentInstance = parentScope.GetService<ISimpleService>();
        var childInstance = childScope.GetService<ISimpleService>();

        // Assert
        await Assert.That(parentInstance.InstanceId).IsNotEqualTo(childInstance.InstanceId);
    }

    #endregion

    #region Generic Register with Lifetime Parameter

    [Test]
    public async Task Register_WithLifetimeParameter_Transient_ReturnsNewInstances()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.Register<ISimpleService>(static _ => new SimpleService(), SvcLifetime.Transient);
        await using var scope = container.CreateScope();

        // Act
        var instance1 = scope.GetService<ISimpleService>();
        var instance2 = scope.GetService<ISimpleService>();

        // Assert
        await Assert.That(instance1.InstanceId).IsNotEqualTo(instance2.InstanceId);
    }

    #endregion
}
