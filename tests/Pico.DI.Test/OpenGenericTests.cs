namespace Pico.DI.Test;

public class OpenGenericTests
{
    private readonly ISvcContainer _container;
    private readonly ISvcProvider _provider;

    public OpenGenericTests()
    {
        _container = Bootstrap.CreateContainer();
        _provider = _container.GetProvider();
    }

    #region Generic Service Tests
    [Fact]
    public void OpenGeneric_Registration_ShouldResolveClosedGeneric()
    {
        // Arrange
        _container.RegisterTransient(typeof(IRepository<>), typeof(Repository<>));

        // Act
        var userRepo = _provider.Resolve<IRepository<User>>();
        var productRepo = _provider.Resolve<IRepository<Product>>();

        // Assert
        Assert.IsType<Repository<User>>(userRepo);
        Assert.IsType<Repository<Product>>(productRepo);
        Assert.NotSame(userRepo, productRepo);
    }

    [Fact]
    public void ClosedGeneric_ShouldOverrideOpenGeneric()
    {
        // Arrange
        _container.RegisterTransient(typeof(IRepository<>), typeof(Repository<>));
        _container.RegisterTransient<IRepository<User>, UserRepository>();

        // Act
        var repo = _provider.Resolve<IRepository<User>>();

        // Assert
        Assert.IsType<UserRepository>(repo);
    }

    [Fact]
    public void Generic_WithTypeConstraints_ShouldValidateConstraints()
    {
        // Arrange
        _container.RegisterTransient(typeof(IValidator<>), typeof(EntityValidator<>));

        // Act & Assert
        Assert.NotNull(_provider.Resolve<IValidator<User>>());
    }

    [Fact]
    public void Generic_SingletonLifetime_ShouldWorkProperly()
    {
        // Arrange
        _container.RegisterSingle(typeof(ICache<>), typeof(MemoryCache<>));

        // Act
        var cache1 = _provider.Resolve<ICache<string>>();
        var cache2 = _provider.Resolve<ICache<string>>();
        var cache3 = _provider.Resolve<ICache<int>>();

        // Assert
        Assert.Same(cache1, cache2);
        Assert.NotSame(cache1, cache3);
    }

    [Fact]
    public void Generic_ScopedLifetime_ShouldCreatePerScope()
    {
        // Arrange
        _container.RegisterScoped(typeof(IUnitOfWork<>), typeof(UnitOfWork<>));
        _container.RegisterScoped<User>();

        // Act
        IUnitOfWork<User> scope1Instance;
        using (var scope1 = _provider.CreateScope())
        {
            scope1Instance = scope1.Resolve<IUnitOfWork<User>>();
            var temp = scope1.Resolve<IUnitOfWork<User>>();
            Assert.Same(scope1Instance, temp);
        }

        using (var scope2 = _provider.CreateScope())
        {
            var scope2Instance = scope2.Resolve<IUnitOfWork<User>>();
            Assert.NotSame(scope1Instance, scope2Instance);
        }
    }

    [Fact]
    public void UnregisteredOpenGeneric_ShouldThrowException()
    {
        // Act & Assert
        Assert.Throws<ServiceNotRegisteredException>(
            () => _provider.Resolve<IRepository<string>>()
        );
    }
    #endregion
}

#region Test Generic Interfaces and Classes
public interface IRepository<T>;

public class Repository<T> : IRepository<T>;

public class UserRepository : IRepository<User>;

public interface IValidator<T>
    where T : class;

public class EntityValidator<T> : IValidator<T>
    where T : class;

public interface ICache<T>;

public class MemoryCache<T> : ICache<T>;

public interface IUnitOfWork<T>;

public class UnitOfWork<T> : IUnitOfWork<T>;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
#endregion
