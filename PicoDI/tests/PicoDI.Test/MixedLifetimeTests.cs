namespace PicoDI.Test;

/// <summary>
/// Tests for mixed lifetime scenarios and cross-lifetime dependency injection.
/// </summary>
public class MixedLifetimeTests
{
    #region Transient depending on Singleton

    [Test]
    public async Task TransientDependsOnSingleton_SingletonShared()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<ISimpleService>(static _ => new SimpleService());
        container.RegisterTransient<IServiceWithDependency>(
            static scope => new ServiceWithDependency(scope.GetService<ISimpleService>())
        );
        await using var scope = container.CreateScope();

        // Act
        var service1 = scope.GetService<IServiceWithDependency>();
        var service2 = scope.GetService<IServiceWithDependency>();

        // Assert - Service is new, but dependency is same singleton
        await Assert.That(service1.InstanceId).IsNotEqualTo(service2.InstanceId);
        await Assert.That(service1.Dependency.InstanceId).IsEqualTo(service2.Dependency.InstanceId);
    }

    #endregion

    #region Transient depending on Scoped

    [Test]
    public async Task TransientDependsOnScoped_ScopedSharedWithinScope()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        container.RegisterTransient<IServiceWithDependency>(
            static scope => new ServiceWithDependency(scope.GetService<ISimpleService>())
        );
        await using var scope = container.CreateScope();

        // Act
        var service1 = scope.GetService<IServiceWithDependency>();
        var service2 = scope.GetService<IServiceWithDependency>();

        // Assert - Service is new, but scoped dependency is same within scope
        await Assert.That(service1.InstanceId).IsNotEqualTo(service2.InstanceId);
        await Assert.That(service1.Dependency.InstanceId).IsEqualTo(service2.Dependency.InstanceId);
    }

    [Test]
    public async Task TransientDependsOnScoped_DifferentAcrossScopes()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        container.RegisterTransient<IServiceWithDependency>(
            static scope => new ServiceWithDependency(scope.GetService<ISimpleService>())
        );
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Act
        var service1 = scope1.GetService<IServiceWithDependency>();
        var service2 = scope2.GetService<IServiceWithDependency>();

        // Assert - Different scoped dependencies
        await Assert
            .That(service1.Dependency.InstanceId)
            .IsNotEqualTo(service2.Dependency.InstanceId);
    }

    #endregion

    #region Scoped depending on Singleton

    [Test]
    public async Task ScopedDependsOnSingleton_SingletonSharedAcrossScopes()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<ISimpleService>(static _ => new SimpleService());
        container.RegisterScoped<IServiceWithDependency>(
            static scope => new ServiceWithDependency(scope.GetService<ISimpleService>())
        );
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Act
        var service1 = scope1.GetService<IServiceWithDependency>();
        var service2 = scope2.GetService<IServiceWithDependency>();

        // Assert - Different scoped services, but same singleton dependency
        await Assert.That(service1.InstanceId).IsNotEqualTo(service2.InstanceId);
        await Assert.That(service1.Dependency.InstanceId).IsEqualTo(service2.Dependency.InstanceId);
    }

    #endregion

    #region Scoped depending on Transient

    [Test]
    public async Task ScopedDependsOnTransient_TransientCapturedOnce()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        container.RegisterScoped<IServiceWithDependency>(
            static scope => new ServiceWithDependency(scope.GetService<ISimpleService>())
        );
        await using var scope = container.CreateScope();

        // Act
        var service1 = scope.GetService<IServiceWithDependency>();
        var service2 = scope.GetService<IServiceWithDependency>();
        var directTransient = scope.GetService<ISimpleService>();

        // Assert - Scoped service is same, with same captured transient
        await Assert.That(service1.InstanceId).IsEqualTo(service2.InstanceId);
        await Assert.That(service1.Dependency.InstanceId).IsEqualTo(service2.Dependency.InstanceId);
        // But direct transient resolution creates new instance
        await Assert.That(directTransient.InstanceId).IsNotEqualTo(service1.Dependency.InstanceId);
    }

    #endregion

    #region Singleton depending on Scoped - Captive Dependency

    [Test]
    public async Task SingletonDependsOnScoped_CaptiveDependencyScenario()
    {
        // Arrange - This is a "captive dependency" anti-pattern
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        container.RegisterSingleton<IServiceWithDependency>(
            static scope => new ServiceWithDependency(scope.GetService<ISimpleService>())
        );
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Act
        var service1 = scope1.GetService<IServiceWithDependency>();
        var service2 = scope2.GetService<IServiceWithDependency>();

        // Assert - Singleton captures the scoped service from first resolution
        // This is technically a captive dependency, but the behavior is defined
        await Assert.That(service1.InstanceId).IsEqualTo(service2.InstanceId);
        await Assert.That(service1.Dependency.InstanceId).IsEqualTo(service2.Dependency.InstanceId);
    }

    #endregion

    #region Singleton depending on Transient - Captive Dependency

    [Test]
    public async Task SingletonDependsOnTransient_TransientCapturedForever()
    {
        // Arrange - Another captive dependency scenario
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        container.RegisterSingleton<IServiceWithDependency>(
            static scope => new ServiceWithDependency(scope.GetService<ISimpleService>())
        );
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Act
        var service1 = scope1.GetService<IServiceWithDependency>();
        var service2 = scope2.GetService<IServiceWithDependency>();
        var directTransient1 = scope1.GetService<ISimpleService>();
        var directTransient2 = scope2.GetService<ISimpleService>();

        // Assert - Singleton has same captured transient, direct transients are new
        await Assert.That(service1.Dependency.InstanceId).IsEqualTo(service2.Dependency.InstanceId);
        await Assert.That(directTransient1.InstanceId).IsNotEqualTo(service1.Dependency.InstanceId);
        await Assert.That(directTransient2.InstanceId).IsNotEqualTo(service1.Dependency.InstanceId);
    }

    #endregion

    #region Complex Mixed Lifetime Chain

    [Test]
    public async Task ComplexChain_Singleton_Scoped_Transient()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Level 1: Singleton
        container.RegisterSingleton<ILevelOneService>(static _ => new LevelOneService());

        // Level 2: Scoped (depends on Singleton)
        container.RegisterScoped<ILevelTwoService>(
            static scope => new LevelTwoService(scope.GetService<ILevelOneService>())
        );

        // Level 3: Transient (depends on Scoped)
        container.RegisterTransient<ILevelThreeService>(
            static scope => new LevelThreeService(scope.GetService<ILevelTwoService>())
        );

        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Act
        var s1_instance1 = scope1.GetService<ILevelThreeService>();
        var s1_instance2 = scope1.GetService<ILevelThreeService>();
        var s2_instance1 = scope2.GetService<ILevelThreeService>();

        // Assert
        // Level 3 (Transient): Different instances
        await Assert.That(s1_instance1.InstanceId).IsNotEqualTo(s1_instance2.InstanceId);
        await Assert.That(s1_instance1.InstanceId).IsNotEqualTo(s2_instance1.InstanceId);

        // Level 2 (Scoped): Same within scope, different across scopes
        await Assert
            .That(s1_instance1.LevelTwo.InstanceId)
            .IsEqualTo(s1_instance2.LevelTwo.InstanceId);
        await Assert
            .That(s1_instance1.LevelTwo.InstanceId)
            .IsNotEqualTo(s2_instance1.LevelTwo.InstanceId);

        // Level 1 (Singleton): Same everywhere
        await Assert
            .That(s1_instance1.LevelTwo.LevelOne.InstanceId)
            .IsEqualTo(s2_instance1.LevelTwo.LevelOne.InstanceId);
    }

    #endregion

    #region Mixed GetServices with Different Lifetimes

    [Test]
    public async Task GetServices_MixedLifetimes_EachBehavesCorrectly()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        container.RegisterSingleton<ISimpleService>(static _ => new SimpleService());
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Act
        var services1_call1 = scope1.GetServices<ISimpleService>().ToList();
        var services1_call2 = scope1.GetServices<ISimpleService>().ToList();
        var services2_call1 = scope2.GetServices<ISimpleService>().ToList();

        // Assert - Order: Transient[0], Scoped[1], Singleton[2]
        await Assert.That(services1_call1.Count).IsEqualTo(3);

        // Transient: Different each call
        await Assert
            .That(services1_call1[0].InstanceId)
            .IsNotEqualTo(services1_call2[0].InstanceId);

        // Scoped: Same within scope
        await Assert.That(services1_call1[1].InstanceId).IsEqualTo(services1_call2[1].InstanceId);

        // Scoped: Different across scopes
        await Assert
            .That(services1_call1[1].InstanceId)
            .IsNotEqualTo(services2_call1[1].InstanceId);

        // Singleton: Same everywhere
        await Assert.That(services1_call1[2].InstanceId).IsEqualTo(services2_call1[2].InstanceId);
    }

    #endregion

    #region Child Scope with Mixed Lifetimes

    [Test]
    public async Task ChildScope_MixedLifetimes_BehavesCorrectly()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ILevelOneService>(static _ => new LevelOneService());
        container.RegisterScoped<ILevelTwoService>(
            static scope => new LevelTwoService(scope.GetService<ILevelOneService>())
        );
        container.RegisterSingleton<ILevelThreeService>(
            static scope => new LevelThreeService(scope.GetService<ILevelTwoService>())
        );

        await using var parentScope = container.CreateScope();
        await using var childScope = parentScope.CreateScope();

        // Act
        var parentL3 = parentScope.GetService<ILevelThreeService>();
        var childL3 = childScope.GetService<ILevelThreeService>();
        var parentL2 = parentScope.GetService<ILevelTwoService>();
        var childL2 = childScope.GetService<ILevelTwoService>();

        // Assert
        // Singleton: Same everywhere
        await Assert.That(parentL3.InstanceId).IsEqualTo(childL3.InstanceId);

        // Scoped: Different between parent and child
        await Assert.That(parentL2.InstanceId).IsNotEqualTo(childL2.InstanceId);

        // The singleton's captured scoped dependency is from first resolution
        await Assert.That(parentL3.LevelTwo.InstanceId).IsEqualTo(childL3.LevelTwo.InstanceId);
    }

    #endregion
}
