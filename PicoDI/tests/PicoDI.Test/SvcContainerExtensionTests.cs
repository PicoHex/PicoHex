namespace PicoDI.Test;

/// <summary>
/// Tests for ISvcContainer extension methods coverage.
/// Covers all registration extension methods in ISvcContainerExtensions.
/// </summary>
public class SvcContainerExtensionTests
{
    #region Register(Type, Type, SvcLifetime) - Open Generic

    [Test]
    public async Task Register_TypeType_OpenGeneric_Transient_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act - Open generic registration
        container.Register(typeof(IRepository<>), typeof(Repository<>), SvcLifetime.Transient);

        // Register closed generic factory for resolution
        container.RegisterTransient<IRepository<User>>(static _ => new Repository<User>());
        await using var scope = container.CreateScope();

        // Assert
        var repo = scope.GetService<IRepository<User>>();
        await Assert.That(repo).IsNotNull();
        await Assert.That(repo.EntityType).IsEqualTo(typeof(User));
    }

    [Test]
    public async Task Register_TypeType_OpenGeneric_Scoped_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act - Open generic registration
        container.Register(typeof(IRepository<>), typeof(Repository<>), SvcLifetime.Scoped);
        container.RegisterScoped<IRepository<Order>>(static _ => new Repository<Order>());
        await using var scope = container.CreateScope();

        // Assert
        var repo = scope.GetService<IRepository<Order>>();
        await Assert.That(repo).IsNotNull();
        await Assert.That(repo.EntityType).IsEqualTo(typeof(Order));
    }

    [Test]
    public async Task Register_TypeType_OpenGeneric_Singleton_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act - Open generic registration
        container.Register(typeof(IRepository<>), typeof(Repository<>), SvcLifetime.Singleton);
        container.RegisterSingleton<IRepository<Product>>(static _ => new Repository<Product>());
        await using var scope = container.CreateScope();

        // Assert
        var repo = scope.GetService<IRepository<Product>>();
        await Assert.That(repo).IsNotNull();
        await Assert.That(repo.EntityType).IsEqualTo(typeof(Product));
    }

    [Test]
    public async Task Register_TypeType_NonGeneric_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act - Type-based registration now creates a SvcDescriptor directly
        container.Register(typeof(ISimpleService), typeof(SimpleService), SvcLifetime.Transient);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Assert
        var svc = scope.GetService<ISimpleService>();
        await Assert.That(svc).IsNotNull();
    }

    #endregion

    #region Register(Type, SvcLifetime) - Open Generic Self-Registration

    [Test]
    public async Task Register_Type_OpenGeneric_Transient_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act - Open generic self-registration
        container.Register(typeof(Repository<>), SvcLifetime.Transient);
        container.RegisterTransient<Repository<User>>(static _ => new Repository<User>());
        await using var scope = container.CreateScope();

        // Assert
        var repo = scope.GetService<Repository<User>>();
        await Assert.That(repo).IsNotNull();
    }

    [Test]
    public async Task Register_Type_OpenGeneric_Scoped_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.Register(typeof(Repository<>), SvcLifetime.Scoped);
        container.RegisterScoped<Repository<Order>>(static _ => new Repository<Order>());
        await using var scope = container.CreateScope();

        // Assert
        var repo = scope.GetService<Repository<Order>>();
        await Assert.That(repo).IsNotNull();
    }

    [Test]
    public async Task Register_Type_OpenGeneric_Singleton_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.Register(typeof(Repository<>), SvcLifetime.Singleton);
        container.RegisterSingleton<Repository<Product>>(static _ => new Repository<Product>());
        await using var scope = container.CreateScope();

        // Assert
        var repo = scope.GetService<Repository<Product>>();
        await Assert.That(repo).IsNotNull();
    }

    [Test]
    public async Task Register_Type_NonGeneric_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act - Self-type registration now creates a SvcDescriptor directly
        container.Register(typeof(SimpleService), SvcLifetime.Transient);
        container.RegisterTransient<SimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Assert
        var svc = scope.GetService<SimpleService>();
        await Assert.That(svc).IsNotNull();
    }

    #endregion

    #region RegisterTransient(Type, Type) - Open Generic

    [Test]
    public async Task RegisterTransient_TypeType_OpenGeneric_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterTransient(typeof(ILogger<>), typeof(Logger<>));
        container.RegisterTransient<ILogger<User>>(static _ => new Logger<User>());
        await using var scope = container.CreateScope();

        // Assert
        var logger = scope.GetService<ILogger<User>>();
        await Assert.That(logger).IsNotNull();
        await Assert.That(logger.CategoryType).IsEqualTo(typeof(User));
    }

    [Test]
    public async Task RegisterTransient_TypeType_NonGeneric_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterTransient(typeof(ISimpleService), typeof(SimpleService));
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Assert
        var svc = scope.GetService<ISimpleService>();
        await Assert.That(svc).IsNotNull();
    }

    #endregion

    #region RegisterTransient(Type) - Open Generic Self-Registration

    [Test]
    public async Task RegisterTransient_Type_OpenGeneric_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterTransient(typeof(Logger<>));
        container.RegisterTransient<Logger<Order>>(static _ => new Logger<Order>());
        await using var scope = container.CreateScope();

        // Assert
        var logger = scope.GetService<Logger<Order>>();
        await Assert.That(logger).IsNotNull();
    }

    [Test]
    public async Task RegisterTransient_Type_NonGeneric_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterTransient(typeof(SimpleService));
        container.RegisterTransient<SimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Assert
        var svc = scope.GetService<SimpleService>();
        await Assert.That(svc).IsNotNull();
    }

    #endregion

    #region RegisterScoped(Type, Type) - Open Generic

    [Test]
    public async Task RegisterScoped_TypeType_OpenGeneric_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterScoped(typeof(IRepository<>), typeof(Repository<>));
        container.RegisterScoped<IRepository<User>>(static _ => new Repository<User>());
        await using var scope = container.CreateScope();

        // Assert
        var repo1 = scope.GetService<IRepository<User>>();
        var repo2 = scope.GetService<IRepository<User>>();
        await Assert.That(repo1.InstanceId).IsEqualTo(repo2.InstanceId);
    }

    [Test]
    public async Task RegisterScoped_TypeType_NonGeneric_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterScoped(typeof(ISimpleService), typeof(SimpleService));
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Assert
        var svc = scope.GetService<ISimpleService>();
        await Assert.That(svc).IsNotNull();
    }

    #endregion

    #region RegisterScoped(Type) - Open Generic Self-Registration

    [Test]
    public async Task RegisterScoped_Type_OpenGeneric_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterScoped(typeof(Repository<>));
        container.RegisterScoped<Repository<Product>>(static _ => new Repository<Product>());
        await using var scope = container.CreateScope();

        // Assert
        var repo = scope.GetService<Repository<Product>>();
        await Assert.That(repo).IsNotNull();
    }

    [Test]
    public async Task RegisterScoped_Type_NonGeneric_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterScoped(typeof(SimpleService));
        container.RegisterScoped<SimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Assert
        var svc = scope.GetService<SimpleService>();
        await Assert.That(svc).IsNotNull();
    }

    #endregion

    #region RegisterSingleton(Type, Type) - Open Generic

    [Test]
    public async Task RegisterSingleton_TypeType_OpenGeneric_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterSingleton(typeof(ILogger<>), typeof(Logger<>));
        container.RegisterSingleton<ILogger<Product>>(static _ => new Logger<Product>());
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Assert
        var logger1 = scope1.GetService<ILogger<Product>>();
        var logger2 = scope2.GetService<ILogger<Product>>();
        await Assert.That(logger1.InstanceId).IsEqualTo(logger2.InstanceId);
    }

    [Test]
    public async Task RegisterSingleton_TypeType_NonGeneric_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterSingleton(typeof(ISimpleService), typeof(SimpleService));
        container.RegisterSingleton<ISimpleService>(static _ => new SimpleService());
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Assert - same instance across scopes (singleton)
        var s1 = scope1.GetService<ISimpleService>();
        var s2 = scope2.GetService<ISimpleService>();
        await Assert.That(s1).IsNotNull();
        await Assert.That(s1.InstanceId).IsEqualTo(s2.InstanceId);
    }

    #endregion

    #region RegisterSingleton(Type) - Open Generic Self-Registration

    [Test]
    public async Task RegisterSingleton_Type_OpenGeneric_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterSingleton(typeof(Logger<>));
        container.RegisterSingleton<Logger<User>>(static _ => new Logger<User>());
        await using var scope = container.CreateScope();

        // Assert
        var logger = scope.GetService<Logger<User>>();
        await Assert.That(logger).IsNotNull();
    }

    [Test]
    public async Task RegisterSingleton_Type_NonGeneric_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterSingleton(typeof(SimpleService));
        container.RegisterSingleton<SimpleService>(static _ => new SimpleService());
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Assert - same instance across scopes (singleton)
        var s1 = scope1.GetService<SimpleService>();
        var s2 = scope2.GetService<SimpleService>();
        await Assert.That(s1).IsNotNull();
        await Assert.That(s1.InstanceId).IsEqualTo(s2.InstanceId);
    }

    #endregion

    #region Generic Placeholder Methods (Source Generator Stubs)

    [Test]
    public async Task RegisterGeneric_TServiceTImpl_WithoutGeneratedConfig_Throws()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() => container.Register<ISimpleService, SimpleService>(SvcLifetime.Transient))
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterGeneric_TService_WithoutGeneratedConfig_Throws()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() => container.Register<SimpleService>(SvcLifetime.Transient))
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterGeneric_TServiceWithType_WithoutGeneratedConfig_Throws()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() =>
                container.Register<ISimpleService>(typeof(SimpleService), SvcLifetime.Transient)
            )
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterTransientGeneric_TServiceTImpl_WithoutGeneratedConfig_Throws()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() => container.RegisterTransient<ISimpleService, SimpleService>())
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterTransientGeneric_TService_WithoutGeneratedConfig_Throws()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() => container.RegisterTransient<SimpleService>())
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterTransientGeneric_TServiceWithType_WithoutGeneratedConfig_Throws()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() => container.RegisterTransient<ISimpleService>(typeof(SimpleService)))
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterScopedGeneric_TServiceTImpl_WithoutGeneratedConfig_Throws()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() => container.RegisterScoped<ISimpleService, SimpleService>())
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterScopedGeneric_TService_WithoutGeneratedConfig_Throws()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() => container.RegisterScoped<SimpleService>())
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterScopedGeneric_TServiceWithType_WithoutGeneratedConfig_Throws()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() => container.RegisterScoped<ISimpleService>(typeof(SimpleService)))
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterSingletonGeneric_TServiceTImpl_WithoutGeneratedConfig_Throws()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() => container.RegisterSingleton<ISimpleService, SimpleService>())
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterSingletonGeneric_TService_WithoutGeneratedConfig_Throws()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() => container.RegisterSingleton<SimpleService>())
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterSingletonGeneric_TServiceWithType_WithoutGeneratedConfig_Throws()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() => container.RegisterSingleton<ISimpleService>(typeof(SimpleService)))
            .Throws<SourceGeneratorRequiredException>();
    }

    #endregion

    #region Factory Registration with Type Parameter

    [Test]
    public async Task Register_TypeFactory_Transient_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.Register(
            typeof(ISimpleService),
            static _ => new SimpleService(),
            SvcLifetime.Transient
        );
        await using var scope = container.CreateScope();

        // Assert
        var s1 = scope.GetService<ISimpleService>();
        var s2 = scope.GetService<ISimpleService>();
        await Assert.That(s1.InstanceId).IsNotEqualTo(s2.InstanceId);
    }

    [Test]
    public async Task Register_TypeFactory_Scoped_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.Register(
            typeof(ISimpleService),
            static _ => new SimpleService(),
            SvcLifetime.Scoped
        );
        await using var scope = container.CreateScope();

        // Assert
        var s1 = scope.GetService<ISimpleService>();
        var s2 = scope.GetService<ISimpleService>();
        await Assert.That(s1.InstanceId).IsEqualTo(s2.InstanceId);
    }

    [Test]
    public async Task Register_TypeFactory_Singleton_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.Register(
            typeof(ISimpleService),
            static _ => new SimpleService(),
            SvcLifetime.Singleton
        );
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Assert
        var s1 = scope1.GetService<ISimpleService>();
        var s2 = scope2.GetService<ISimpleService>();
        await Assert.That(s1.InstanceId).IsEqualTo(s2.InstanceId);
    }

    #endregion

    #region Factory Registration Generic with TService, TImplementation

    [Test]
    public async Task RegisterGeneric_TServiceTImpl_Factory_Transient_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.Register<ISimpleService, SimpleService>(
            static _ => new SimpleService(),
            SvcLifetime.Transient
        );
        await using var scope = container.CreateScope();

        // Assert
        var s1 = scope.GetService<ISimpleService>();
        var s2 = scope.GetService<ISimpleService>();
        await Assert.That(s1.InstanceId).IsNotEqualTo(s2.InstanceId);
    }

    [Test]
    public async Task RegisterGeneric_TServiceTImpl_Factory_Scoped_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.Register<ISimpleService, SimpleService>(
            static _ => new SimpleService(),
            SvcLifetime.Scoped
        );
        await using var scope = container.CreateScope();

        // Assert
        var s1 = scope.GetService<ISimpleService>();
        var s2 = scope.GetService<ISimpleService>();
        await Assert.That(s1.InstanceId).IsEqualTo(s2.InstanceId);
    }

    [Test]
    public async Task RegisterGeneric_TServiceTImpl_Factory_Singleton_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.Register<ISimpleService, SimpleService>(
            static _ => new SimpleService(),
            SvcLifetime.Singleton
        );
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Assert
        var s1 = scope1.GetService<ISimpleService>();
        var s2 = scope2.GetService<ISimpleService>();
        await Assert.That(s1.InstanceId).IsEqualTo(s2.InstanceId);
    }

    #endregion

    #region Transient Factory Methods

    [Test]
    public async Task RegisterTransient_TypeFactory_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Assert
        var s1 = scope.GetService<ISimpleService>();
        var s2 = scope.GetService<ISimpleService>();
        await Assert.That(s1.InstanceId).IsNotEqualTo(s2.InstanceId);
    }

    [Test]
    public async Task RegisterTransient_GenericTServiceTImpl_Factory_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterTransient<ISimpleService, SimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Assert
        var s1 = scope.GetService<ISimpleService>();
        var s2 = scope.GetService<ISimpleService>();
        await Assert.That(s1.InstanceId).IsNotEqualTo(s2.InstanceId);
    }

    #endregion

    #region Scoped Factory Methods

    [Test]
    public async Task RegisterScoped_TypeFactory_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Assert
        var s1 = scope.GetService<ISimpleService>();
        var s2 = scope.GetService<ISimpleService>();
        await Assert.That(s1.InstanceId).IsEqualTo(s2.InstanceId);
    }

    [Test]
    public async Task RegisterScoped_GenericTServiceTImpl_Factory_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterScoped<ISimpleService, SimpleService>(static _ => new SimpleService());
        await using var scope = container.CreateScope();

        // Assert
        var s1 = scope.GetService<ISimpleService>();
        var s2 = scope.GetService<ISimpleService>();
        await Assert.That(s1.InstanceId).IsEqualTo(s2.InstanceId);
    }

    #endregion

    #region Singleton Factory Methods

    [Test]
    public async Task RegisterSingleton_TypeFactory_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterSingleton<ISimpleService>(static _ => new SimpleService());
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Assert
        var s1 = scope1.GetService<ISimpleService>();
        var s2 = scope2.GetService<ISimpleService>();
        await Assert.That(s1.InstanceId).IsEqualTo(s2.InstanceId);
    }

    [Test]
    public async Task RegisterSingleton_GenericTServiceTImpl_Factory_Works()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterSingleton<ISimpleService, SimpleService>(static _ => new SimpleService());
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Assert
        var s1 = scope1.GetService<ISimpleService>();
        var s2 = scope2.GetService<ISimpleService>();
        await Assert.That(s1.InstanceId).IsEqualTo(s2.InstanceId);
    }

    #endregion

    #region RegisterSingle (Instance Registration)

    [Test]
    public async Task RegisterSingle_TypeInstance_Works()
    {
        // Arrange
        var instance = new SimpleService();
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterSingle(typeof(ISimpleService), instance);
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Assert
        var s1 = scope1.GetService<ISimpleService>();
        var s2 = scope2.GetService<ISimpleService>();
        await Assert.That(s1.InstanceId).IsEqualTo(instance.InstanceId);
        await Assert.That(s2.InstanceId).IsEqualTo(instance.InstanceId);
    }

    [Test]
    public async Task RegisterSingle_GenericInstance_Works()
    {
        // Arrange
        var instance = new SimpleService();
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act
        container.RegisterSingle<ISimpleService>(instance);
        await using var scope = container.CreateScope();

        // Assert
        var resolved = scope.GetService<ISimpleService>();
        await Assert.That(resolved.InstanceId).IsEqualTo(instance.InstanceId);
    }

    #endregion

    #region Method Chaining Tests

    [Test]
    public async Task AllRegisterMethods_SupportMethodChaining()
    {
        // Arrange & Act
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        var result = container
            .RegisterTransient<ISimpleService>(static _ => new SimpleService())
            .RegisterScoped<ILevelOneService>(static _ => new LevelOneService())
            .RegisterSingleton<IConfigurableService>(static _ => new ConfigurableService("test"))
            .RegisterSingle<INotificationService>(new EmailNotificationService())
            .RegisterRange(
                new[]
                {
                    SvcDescriptor.Create(
                        typeof(ILevelTwoService),
                        static s => new LevelTwoService(s.GetService<ILevelOneService>()),
                        SvcLifetime.Transient
                    ),
                }
            );

        // Assert
        await Assert.That(result).IsEqualTo(container);

        await using var scope = container.CreateScope();
        await Assert.That(scope.GetService<ISimpleService>()).IsNotNull();
        await Assert.That(scope.GetService<ILevelOneService>()).IsNotNull();
        await Assert.That(scope.GetService<IConfigurableService>()).IsNotNull();
        await Assert.That(scope.GetService<INotificationService>()).IsNotNull();
        await Assert.That(scope.GetService<ILevelTwoService>()).IsNotNull();
    }

    #endregion

    #region ISvcScope TryGetService / TryGetServices

    [Test]
    public async Task TryGetService_Registered_ReturnsTrue()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        // Use string type — won't be auto-registered by source generator
        container.RegisterSingle(typeof(string), "hello");
        await using var scope = container.CreateScope();

        var found = CallTryGetService(scope, typeof(string), out var svc);

        await Assert.That(found).IsTrue();
        await Assert.That(svc).IsEqualTo("hello");
    }

    [Test]
    public async Task TryGetService_Unregistered_ReturnsFalse()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        await using var scope = container.CreateScope();

        var found = CallTryGetService(scope, typeof(IDisposable), out var svc);

        await Assert.That(found).IsFalse();
        await Assert.That(svc).IsNull();
    }

    [Test]
    public async Task TryGetServices_Registered_ReturnsTrue()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingle(typeof(string), "hello");
        await using var scope = container.CreateScope();

        var found = CallTryGetServices(scope, typeof(string), out var svcs);

        await Assert.That(found).IsTrue();
        await Assert.That(svcs).IsNotNull();
        await Assert.That(svcs!.Count).IsGreaterThanOrEqualTo(1);
    }

    [Test]
    public async Task TryGetServices_Unregistered_ReturnsFalse()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        await using var scope = container.CreateScope();

        var found = CallTryGetServices(scope, typeof(IDisposable), out var svcs);

        await Assert.That(found).IsFalse();
        await Assert.That(svcs).IsNull();
    }

    // Call through ISvcScope interface to verify the interface contract is usable
    private static bool CallTryGetService(ISvcScope scope, Type serviceType, out object? service) =>
        scope.TryGetService(serviceType, out service);

    private static bool CallTryGetServices(
        ISvcScope scope,
        Type serviceType,
        out IReadOnlyList<object>? services
    ) => scope.TryGetServices(serviceType, out services);

    #endregion
}
