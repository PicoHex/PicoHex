namespace PicoDI.Test;

/// <summary>
/// Tests for error handling and edge cases.
/// </summary>
public class ErrorHandlingTests
{
    #region Unregistered Service Tests

    [Test]
    public async Task GetService_UnregisteredService_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        await using var scope = container.CreateScope();

        // Act & Assert
        await Assert.That(() => scope.GetService<ISimpleService>()).Throws<Exception>(); // PicoDiException or KeyNotFoundException
    }

    [Test]
    public async Task GetServices_UnregisteredService_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        await using var scope = container.CreateScope();

        // Act & Assert - GetServices also throws for unregistered service
        await Assert
            .That(() => scope.GetServices<ISimpleService>().ToList())
            .Throws<PicoDiException>();
    }

    #endregion

    #region Factory Exception Tests

    [Test]
    public async Task GetService_FactoryThrows_ExceptionPropagates()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(
            static _ => throw new InvalidOperationException("Factory failed")
        );
        await using var scope = container.CreateScope();

        // Act & Assert
        await Assert
            .That(() => scope.GetService<ISimpleService>())
            .Throws<InvalidOperationException>()
            .WithMessage("Factory failed");
    }

    [Test]
    public async Task GetService_SingletonFactoryThrows_SameExceptionOnRetry()
    {
        // Arrange
        var callCount = 0;
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<ISimpleService>(_ =>
        {
            callCount++;
            throw new InvalidOperationException($"Factory failed attempt {callCount}");
        });
        await using var scope = container.CreateScope();

        // Act & Assert - Each retry calls the factory again
        await Assert
            .That(() => scope.GetService<ISimpleService>())
            .Throws<InvalidOperationException>();

        await Assert
            .That(() => scope.GetService<ISimpleService>())
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GetService_ScopedFactoryThrows_ExceptionOnEachScope()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(
            static _ => throw new InvalidOperationException("Scoped factory failed")
        );

        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Act & Assert - Both scopes should fail
        await Assert
            .That(() => scope1.GetService<ISimpleService>())
            .Throws<InvalidOperationException>();

        await Assert
            .That(() => scope2.GetService<ISimpleService>())
            .Throws<InvalidOperationException>();
    }

    #endregion

    #region Null Handling Tests

    [Test]
    public async Task Register_NullDescriptor_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert.That(() => container.Register(null!)).Throws<Exception>(); // ArgumentNullException or NullReferenceException
    }

    [Test]
    public async Task Register_NullFactory_ThrowsException()
    {
        // Act & Assert
        await Assert
            .That(() => new SvcDescriptor(typeof(ISimpleService), (Func<ISvcScope, object>)null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task RegisterSingle_NullInstance_ThrowsException()
    {
        // Act & Assert
        await Assert
            .That(() => SvcDescriptor.FromInstance(typeof(ISimpleService), null!))
            .Throws<ArgumentNullException>();
    }

    #endregion

    #region Type Placeholder Registration Tests

    [Test]
    public async Task RegisterPlaceholder_WithoutSourceGenerator_ThrowsOnResolve()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert - Placeholder registrations now fail fast unless generated
        // registrations were applied to this specific container.
        await Assert
            .That(() => container.RegisterTransient<ISimpleService>())
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterOpenGeneric_NonGenericType_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert - Non-generic types should throw
        await Assert
            .That(() => container.RegisterTransient(typeof(ISimpleService), typeof(SimpleService)))
            .Throws<InvalidOperationException>();
    }

    #endregion

    #region Disposed Scope Tests

    [Test]
    public async Task DisposedScope_GetService_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());
        var scope = container.CreateScope();
        await scope.DisposeAsync();

        // Act & Assert
        await Assert
            .That(() => scope.GetService<ISimpleService>())
            .Throws<ObjectDisposedException>();
    }

    [Test]
    public async Task DisposedScope_CreateChildScope_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        var scope = container.CreateScope();
        await scope.DisposeAsync();

        // Act & Assert
        await Assert.That(() => scope.CreateScope()).Throws<ObjectDisposedException>();
    }

    #endregion

    #region Edge Case Tests

    [Test]
    public async Task EmptyContainer_Build_NoError()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert - Should not throw
        container.Build();
        // No assertion needed - if we get here, no exception was thrown
    }

    [Test]
    public async Task EmptyContainer_CreateScope_NoError()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert - Should not throw
        await using var scope = container.CreateScope();
        await Assert.That(scope).IsNotNull();
    }

    [Test]
    public async Task RegisterSameServiceMultipleTimes_LastWins()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<IConfigurableService>(
            static _ => new ConfigurableService("first")
        );
        container.RegisterTransient<IConfigurableService>(
            static _ => new ConfigurableService("second")
        );
        container.RegisterTransient<IConfigurableService>(
            static _ => new ConfigurableService("third")
        );
        await using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<IConfigurableService>();

        // Assert - GetService returns the last registered
        await Assert.That(service.Configuration).IsEqualTo("third");
    }

    [Test]
    public async Task RegisterSameServiceMultipleTimes_GetServicesReturnsAll()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<IConfigurableService>(
            static _ => new ConfigurableService("first")
        );
        container.RegisterTransient<IConfigurableService>(
            static _ => new ConfigurableService("second")
        );
        container.RegisterTransient<IConfigurableService>(
            static _ => new ConfigurableService("third")
        );
        await using var scope = container.CreateScope();

        // Act
        var services = scope.GetServices<IConfigurableService>().ToList();

        // Assert
        await Assert.That(services.Count).IsEqualTo(3);
        var configurations = services.Select(s => s.Configuration).ToArray();
        await Assert.That(configurations.Length).IsEqualTo(3);
        await Assert.That(configurations[0]).IsEqualTo("first");
        await Assert.That(configurations[1]).IsEqualTo("second");
        await Assert.That(configurations[2]).IsEqualTo("third");
    }

    #endregion

    #region Exception Class Tests

    [Test]
    public async Task PicoDiException_AllConstructors_WorkCorrectly()
    {
        // Arrange & Act 1: Default constructor
        var ex1 = new PicoDiException();
        await Assert.That(ex1.Message).IsNotNull();

        // Arrange & Act 2: Message constructor
        const string message = "Test message";
        var ex2 = new PicoDiException(message);
        await Assert.That(ex2.Message).IsEqualTo(message);

        // Arrange & Act 3: Message with inner exception
        var innerEx = new InvalidOperationException("Inner exception");
        var ex3 = new PicoDiException(message, innerEx);
        await Assert.That(ex3.Message).IsEqualTo(message);
        await Assert.That(ex3.InnerException).IsEqualTo(innerEx);
    }

    [Test]
    public async Task SourceGeneratorRequiredException_AllConstructors_WorkCorrectly()
    {
        // Arrange & Act 1: Default constructor
        var ex1 = new SourceGeneratorRequiredException();
        await Assert.That(ex1.Message).IsNotNull();
        await Assert
            .That(ex1.Message)
            .Contains("Compile-time generated registrations are required");

        // Arrange & Act 2: Message constructor
        const string message = "Custom message";
        var ex2 = new SourceGeneratorRequiredException(message);
        await Assert.That(ex2.Message).IsEqualTo(message);

        // Arrange & Act 3: Message with inner exception
        var innerEx = new InvalidOperationException("Inner exception");
        var ex3 = new SourceGeneratorRequiredException(message, innerEx);
        await Assert.That(ex3.Message).IsEqualTo(message);
        await Assert.That(ex3.InnerException).IsEqualTo(innerEx);
    }

    #endregion

    #region Generic Registration Method Tests

    [Test]
    public async Task RegisterTransientGeneric_NullImplementationType_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() => container.RegisterTransient<ISimpleService>((Type)null!))
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterScopedGeneric_NullImplementationType_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() => container.RegisterScoped<ISimpleService>((Type)null!))
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterSingletonGeneric_NullImplementationType_ThrowsException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert
        await Assert
            .That(() => container.RegisterSingleton<ISimpleService>((Type)null!))
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterTransientGeneric_TServiceOnly_WorksWithValidFactory()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterTransient<ISimpleService>(static _ => new SimpleService());

        // Act
        await using var scope = container.CreateScope();
        var service = scope.GetService<ISimpleService>();

        // Assert
        await Assert.That(service).IsNotNull();
    }

    [Test]
    public async Task RegisterScopedGeneric_TServiceOnly_WorksWithValidFactory()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterScoped<ISimpleService>(static _ => new SimpleService());

        // Act
        await using var scope = container.CreateScope();
        var service = scope.GetService<ISimpleService>();

        // Assert
        await Assert.That(service).IsNotNull();
    }

    [Test]
    public async Task RegisterSingletonGeneric_TServiceOnly_WorksWithValidFactory()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<ISimpleService>(static _ => new SimpleService());

        // Act
        await using var scope = container.CreateScope();
        var service = scope.GetService<ISimpleService>();

        // Assert
        await Assert.That(service).IsNotNull();
    }

    [Test]
    public async Task RegisterTransientGeneric_TServiceTImplementation_ValidTypes()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        await Assert
            .That(() => container.RegisterTransient<ISimpleService, SimpleService>())
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterScopedGeneric_TServiceTImplementation_ValidTypes()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        await Assert
            .That(() => container.RegisterScoped<ISimpleService, SimpleService>())
            .Throws<SourceGeneratorRequiredException>();
    }

    [Test]
    public async Task RegisterSingletonGeneric_TServiceTImplementation_ValidTypes()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        await Assert
            .That(() => container.RegisterSingleton<ISimpleService, SimpleService>())
            .Throws<SourceGeneratorRequiredException>();
    }

    #endregion

    #region Source Generator Integration Tests

    /// <summary>
    /// This static method contains generic registration calls that the source generator
    /// will detect and generate factory methods for.
    /// The source generator scans all Register* method calls in the project.
    /// </summary>
    public static void RegisterServicesForSourceGenerator(ISvcContainer container)
    {
        // These calls will be detected by the source generator
        container.RegisterTransient<ISimpleService, SimpleService>();
        container.RegisterScoped<IServiceWithDependency, ServiceWithDependency>();
        container.RegisterSingleton<IRepository<User>, Repository<User>>();
    }

    [Test]
    public async Task GenericRegistration_WithSourceGenerator_ActuallyRegistersServices()
    {
        if (!SvcContainerAutoConfiguration.HasConfigurator)
        {
            return;
        }

        await using var container = new SvcContainer(autoConfigureFromGenerator: true);
        await using var scope = container.CreateScope();
        var service = scope.GetService<ISimpleService>();

        await Assert.That(service).IsNotNull();
        await Assert.That(service is SimpleService).IsTrue();
    }

    [Test]
    public async Task PlaceholderRegistration_WithAppliedGeneratedConfiguration_ReturnsContainer()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        SvcContainerAutoConfiguration.MarkGeneratedConfigurationApplied(container);

        var result = container.RegisterTransient<ISimpleService, SimpleService>();

        await Assert.That(result).IsEqualTo(container);
    }

    [Test]
    public async Task MarkGeneratedConfigurationApplied_SvcContainerCapability_IsUpdated()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        await Assert
            .That(SvcContainerAutoConfiguration.HasAppliedGeneratedConfiguration(container))
            .IsFalse();

        SvcContainerAutoConfiguration.MarkGeneratedConfigurationApplied(container);

        await Assert
            .That(SvcContainerAutoConfiguration.HasAppliedGeneratedConfiguration(container))
            .IsTrue();
    }

    [Test]
    public async Task PlaceholderRegistration_WithAppliedGeneratedConfiguration_FallbackContainer_ReturnsContainer()
    {
        await using var container = new FakeContainer();

        SvcContainerAutoConfiguration.MarkGeneratedConfigurationApplied(container);

        var result = container.RegisterTransient<ISimpleService, SimpleService>();

        await Assert.That(result).IsEqualTo(container);
        await Assert
            .That(SvcContainerAutoConfiguration.HasAppliedGeneratedConfiguration(container))
            .IsTrue();
        await Assert.That(SvcContainerAutoConfiguration.TryApplyConfiguration(container)).IsFalse();
    }

    [Test]
    public async Task MarkGeneratedConfigurationApplied_DoesNotApplyConfigurators()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        SvcContainerAutoConfiguration.RegisterConfigurator(
            "task3-state-only-mark",
            static c =>
                c.RegisterSingleton<IStateOnlyGeneratedConfigurationProbe>(
                    _ => new StateOnlyGeneratedConfigurationProbe("configured")
                )
        );

        SvcContainerAutoConfiguration.MarkGeneratedConfigurationApplied(container);

        await Assert
            .That(SvcContainerAutoConfiguration.HasAppliedGeneratedConfiguration(container))
            .IsTrue();
        await Assert.That(SvcContainerAutoConfiguration.TryApplyConfiguration(container)).IsFalse();

        await using var scope = container.CreateScope();
        await Assert
            .That(() => scope.GetService<IStateOnlyGeneratedConfigurationProbe>())
            .Throws<Exception>();
    }

    #endregion

    #region Disposal and Registration Behavior Tests

    private interface IStateOnlyGeneratedConfigurationProbe
    {
        string Configuration { get; }
    }

    private sealed class StateOnlyGeneratedConfigurationProbe(string configuration)
        : IStateOnlyGeneratedConfigurationProbe
    {
        public string Configuration { get; } = configuration;
    }

    private sealed class FakeContainer : ISvcContainer
    {
        public ISvcContainer Register(SvcDescriptor descriptor)
        {
            return this;
        }

        public ISvcScope CreateScope()
        {
            throw new NotSupportedException();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    [Test]
    public async Task Dispose_FaultyServices_DoNotThrow()
    {
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.RegisterSingleton<FaultyDisposableService>(
            static _ => new FaultyDisposableService()
        );
        container.RegisterSingleton<FaultyAsyncDisposableService>(
            static _ => new FaultyAsyncDisposableService()
        );

        await using (var scope = container.CreateScope())
        {
            _ = scope.GetService<FaultyDisposableService>();
            _ = scope.GetService<FaultyAsyncDisposableService>();
        }

        await container.DisposeAsync();
    }

    [Test]
    public async Task RegisterAfterBuild_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.Build();

        // Act & Assert - attempting to register a service should throw
        await Assert
            .That(() => container.RegisterTransient<ISimpleService>(_ => new SimpleService()))
            .Throws<InvalidOperationException>();
    }

    #endregion
}
