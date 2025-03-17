namespace PicoHex.DependencyInjection.NG.Test
{
    public class ContainerTests
    {
        #region Test Interfaces and Classes
        public interface ITestService
        {
            string GetValue();
        }

        public interface IDisposableService : IDisposable
        {
            bool IsDisposed { get; }
        }

        public interface IDependentService
        {
            ITestService TestService { get; }
        }

        public class TestService : ITestService
        {
            public string GetValue() => "Test";
        }

        public class AnotherTestService : ITestService
        {
            public string GetValue() => "Another";
        }

        public class DisposableService : IDisposableService
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        public class DependentService : IDependentService
        {
            public ITestService TestService { get; }

            public DependentService(ITestService testService)
            {
                TestService = testService;
            }
        }

        public class ServiceWithMultipleDependencies
        {
            public ITestService TestService { get; }
            public IDisposableService DisposableService { get; }

            public ServiceWithMultipleDependencies(
                ITestService testService,
                IDisposableService disposableService
            )
            {
                TestService = testService;
                DisposableService = disposableService;
            }
        }

        public class ThreadTestService : ITestService
        {
            private static int _instanceCount = 0;
            private readonly int _instanceId;

            public ThreadTestService()
            {
                _instanceId = Interlocked.Increment(ref _instanceCount);
            }

            public string GetValue() => $"Instance {_instanceId}";
        }

        public class PooledResource : IDisposable
        {
            private static int _instanceCount = 0;
            private readonly int _instanceId;
            public bool IsDisposed { get; private set; }

            public PooledResource()
            {
                _instanceId = Interlocked.Increment(ref _instanceCount);
            }

            public int InstanceId => _instanceId;

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
        #endregion

        #region Singleton Tests
        [Fact]
        public void Singleton_ReturnsSameInstance()
        {
            // Arrange
            var container = new ContainerBuilder()
                .RegisterSingleton<ITestService, TestService>()
                .Build();

            // Act
            var instance1 = container.GetService<ITestService>();
            var instance2 = container.GetService<ITestService>();

            // Assert
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public void Singleton_WithInstance_ReturnsSameInstance()
        {
            // Arrange
            var testService = new TestService();
            var container = new ContainerBuilder().RegisterSingleton(testService).Build();

            // Act
            var resolvedService = container.GetService<TestService>();

            // Assert
            Assert.Same(testService, resolvedService);
        }
        #endregion

        #region Transient Tests
        [Fact]
        public void Transient_ReturnsDifferentInstances()
        {
            // Arrange
            var container = new ContainerBuilder()
                .RegisterTransient<ITestService, TestService>()
                .Build();

            // Act
            var instance1 = container.GetService<ITestService>();
            var instance2 = container.GetService<ITestService>();

            // Assert
            Assert.NotSame(instance1, instance2);
        }
        #endregion

        #region Scoped Tests
        [Fact]
        public void Scoped_ReturnsNewInstanceForEachScope()
        {
            // Arrange
            var container = new ContainerBuilder()
                .RegisterScoped<ITestService, TestService>()
                .Build();

            // Act
            var rootInstance = container.GetService<ITestService>();

            using var scope1 = container.CreateScope();
            var scope1Instance = scope1.GetService<ITestService>();

            using var scope2 = container.CreateScope();
            var scope2Instance = scope2.GetService<ITestService>();

            // Assert
            Assert.NotSame(rootInstance, scope1Instance);
            Assert.NotSame(rootInstance, scope2Instance);
            Assert.NotSame(scope1Instance, scope2Instance);
        }

        [Fact]
        public void Scoped_ReturnsSameInstanceWithinScope()
        {
            // Arrange
            var container = new ContainerBuilder()
                .RegisterScoped<ITestService, TestService>()
                .Build();

            // Act
            using var scope = container.CreateScope();
            var instance1 = scope.GetService<ITestService>();
            var instance2 = scope.GetService<ITestService>();

            // Assert
            Assert.Same(instance1, instance2);
        }
        #endregion

        #region PerThread Tests
        [Fact]
        public void PerThread_ReturnsSameInstanceWithinThread()
        {
            // Arrange
            var container = new ContainerBuilder()
                .RegisterPerThread<ITestService, ThreadTestService>()
                .Build();

            // Act
            var instance1 = container.GetService<ITestService>();
            var instance2 = container.GetService<ITestService>();

            // Assert
            Assert.Same(instance1, instance2);
        }

        [Fact]
        public async Task PerThread_ReturnsDifferentInstancesAcrossThreads()
        {
            // Arrange
            var container = new ContainerBuilder()
                .RegisterPerThread<ITestService, ThreadTestService>()
                .Build();

            // Act
            var mainThreadInstance = container.GetService<ITestService>();
            string mainThreadValue = mainThreadInstance.GetValue();

            string threadValue = await Task.Run(() =>
            {
                var threadInstance = container.GetService<ITestService>();
                return threadInstance.GetValue();
            });

            // Assert
            Assert.NotEqual(mainThreadValue, threadValue);
        }
        #endregion

        #region Pooled Tests
        [Fact]
        public void Pooled_ReturnsInstanceFromPool()
        {
            // Arrange
            var container = new ContainerBuilder()
                .RegisterPooled<PooledResource, PooledResource>()
                .Build();

            // Act
            var instance1 = container.GetService<PooledResource>();
            var id1 = instance1.InstanceId;

            // Simulate returning to pool and getting again
            // In a real scenario, we would need a way to return to pool
            // For this test, we're just checking different instances
            var instance2 = container.GetService<PooledResource>();
            var id2 = instance2.InstanceId;

            // Assert
            Assert.NotEqual(id1, id2); // We're creating new instances as we don't manually return them
        }
        #endregion

        #region Dependency Injection Tests
        [Fact]
        public void DependencyInjection_InjectsRegisteredDependencies()
        {
            // Arrange
            var container = new ContainerBuilder()
                .RegisterSingleton<ITestService, TestService>()
                .RegisterTransient<IDependentService, DependentService>()
                .Build();

            // Act
            var service = container.GetService<IDependentService>();

            // Assert
            Assert.NotNull(service);
            Assert.NotNull(service.TestService);
            Assert.Equal("Test", service.TestService.GetValue());
        }

        [Fact]
        public void DependencyInjection_InjectsMultipleDependencies()
        {
            // Arrange
            var container = new ContainerBuilder()
                .RegisterSingleton<ITestService, TestService>()
                .RegisterSingleton<IDisposableService, DisposableService>()
                .RegisterTransient<
                    ServiceWithMultipleDependencies,
                    ServiceWithMultipleDependencies
                >()
                .Build();

            // Act
            var service = container.GetService<ServiceWithMultipleDependencies>();

            // Assert
            Assert.NotNull(service);
            Assert.NotNull(service.TestService);
            Assert.NotNull(service.DisposableService);
            Assert.Equal("Test", service.TestService.GetValue());
        }
        #endregion

        #region AOT Tests
        [Fact]
        public void AotFactory_CreatesInstance()
        {
            // Arrange
            var container = new ContainerBuilder()
                .RegisterAotFactory<ITestService>(_ => new TestService())
                .Build();

            // Act
            var service = container.GetService<ITestService>();

            // Assert
            Assert.NotNull(service);
            Assert.Equal("Test", service.GetValue());
        }

        [Fact]
        public void AotFactory_WithDependencies_CreatesInstance()
        {
            // Arrange
            var container = new ContainerBuilder()
                .RegisterSingleton<ITestService, TestService>()
                .RegisterAotFactory<IDependentService>(
                    sp => new DependentService((ITestService)sp.GetService(typeof(ITestService)))
                )
                .Build();

            // Act
            var service = container.GetService<IDependentService>();

            // Assert
            Assert.NotNull(service);
            Assert.NotNull(service.TestService);
            Assert.Equal("Test", service.TestService.GetValue());
        }
        #endregion

        #region Error Handling Tests
        [Fact]
        public void GetService_UnregisteredService_ThrowsException()
        {
            // Arrange
            var container = new Container();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => container.GetService<ITestService>());
        }

        [Fact]
        public void Dispose_DisposesRegisteredSingletons()
        {
            // Arrange
            var container = new ContainerBuilder()
                .RegisterSingleton<IDisposableService, DisposableService>()
                .Build();

            // Act
            var service = container.GetService<IDisposableService>();
            Assert.False(service.IsDisposed);

            // Clean up
            ((IDisposable)container).Dispose();

            // Assert
            Assert.True(service.IsDisposed);
        }
        #endregion

        #region Multiple Registration Tests
        [Fact]
        public void MultipleRegistrations_LastRegistrationWins()
        {
            // Arrange
            var container = new ContainerBuilder()
                .RegisterSingleton<ITestService, TestService>()
                .RegisterSingleton<ITestService, AnotherTestService>() // This overrides the previous registration
                .Build();

            // Act
            var service = container.GetService<ITestService>();

            // Assert
            Assert.IsType<AnotherTestService>(service);
            Assert.Equal("Another", service.GetValue());
        }
        #endregion
    }
}
