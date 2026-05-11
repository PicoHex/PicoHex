namespace PicoDI.Test;

#region Basic Service Interfaces and Implementations

public interface ISimpleService
{
    Guid InstanceId { get; }
}

public class SimpleService : ISimpleService
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public interface IServiceWithDependency
{
    ISimpleService Dependency { get; }
    Guid InstanceId { get; }
}

public class ServiceWithDependency(ISimpleService dependency) : IServiceWithDependency
{
    public ISimpleService Dependency { get; } = dependency;
    public Guid InstanceId { get; } = Guid.NewGuid();
}

#endregion

#region Disposable Services

public interface IDisposableService : IDisposable
{
    Guid InstanceId { get; }
    bool IsDisposed { get; }
}

public class DisposableService : IDisposableService
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public bool IsDisposed { get; private set; }

    public void Dispose() => IsDisposed = true;
}

public interface IAsyncDisposableService : IAsyncDisposable
{
    Guid InstanceId { get; }
    bool IsDisposed { get; }
}

public class AsyncDisposableService : IAsyncDisposableService
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public bool IsDisposed { get; private set; }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}

#endregion

#region Open Generic Services

public interface IRepository<T>
{
    Guid InstanceId { get; }
    Type EntityType { get; }
}

public class Repository<T> : IRepository<T>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public Type EntityType => typeof(T);
}

public interface ILogger<T>
{
    Guid InstanceId { get; }
    Type CategoryType { get; }
}

public class Logger<T> : ILogger<T>
{
    public Guid InstanceId { get; } = Guid.NewGuid();
    public Type CategoryType => typeof(T);
}

// Entity types for open generic tests
public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}

public class Order
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
}

#endregion

#region Multiple Implementation Services

public interface INotificationService
{
    string NotificationType { get; }
    Guid InstanceId { get; }
}

public class EmailNotificationService : INotificationService
{
    public string NotificationType => "Email";
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public class SmsNotificationService : INotificationService
{
    public string NotificationType => "SMS";
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public class PushNotificationService : INotificationService
{
    public string NotificationType => "Push";
    public Guid InstanceId { get; } = Guid.NewGuid();
}

#endregion

#region Complex Dependency Chain Services

public interface ILevelOneService
{
    Guid InstanceId { get; }
}

public class LevelOneService : ILevelOneService
{
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public interface ILevelTwoService
{
    ILevelOneService LevelOne { get; }
    Guid InstanceId { get; }
}

public class LevelTwoService(ILevelOneService levelOne) : ILevelTwoService
{
    public ILevelOneService LevelOne { get; } = levelOne;
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public interface ILevelThreeService
{
    ILevelTwoService LevelTwo { get; }
    Guid InstanceId { get; }
}

public class LevelThreeService(ILevelTwoService levelTwo) : ILevelThreeService
{
    public ILevelTwoService LevelTwo { get; } = levelTwo;
    public Guid InstanceId { get; } = Guid.NewGuid();
}

#endregion

#region Services for Factory Tests

public interface IConfigurableService
{
    string Configuration { get; }
    Guid InstanceId { get; }
}

public class ConfigurableService(string config) : IConfigurableService
{
    public string Configuration { get; } = config;
    public Guid InstanceId { get; } = Guid.NewGuid();
}

public interface IAlternativeSimpleService
{
    string ConstructorUsed { get; }
}

public sealed class PreferredCtorDependency;

public class PreferredCtorService : IAlternativeSimpleService
{
    public PreferredCtorService()
    {
        ConstructorUsed = "default";
    }

    [SvcConstructor]
    public PreferredCtorService(PreferredCtorDependency dependency)
    {
        Dependency = dependency;
        ConstructorUsed = "preferred";
    }

    public PreferredCtorDependency? Dependency { get; }
    public string ConstructorUsed { get; }
}

#endregion

#region Fault-Injecting Disposable Services

/// <summary>
/// A service that implements only IDisposable and throws during Dispose().
/// Used to test error-handling paths during synchronous dispose.
/// </summary>
public class FaultyDisposableService : IDisposable
{
    public bool DisposeCalled { get; private set; }

    public void Dispose()
    {
        DisposeCalled = true;
        throw new InvalidOperationException("Dispose failed on purpose");
    }
}

/// <summary>
/// A service that implements only IAsyncDisposable and throws during DisposeAsync().
/// Used to test error-handling paths during asynchronous dispose.
/// </summary>
public class FaultyAsyncDisposableService : IAsyncDisposable
{
    public bool DisposeAsyncCalled { get; private set; }

    public ValueTask DisposeAsync()
    {
        DisposeAsyncCalled = true;
        throw new InvalidOperationException("DisposeAsync failed on purpose");
    }
}

/// <summary>
/// A service that implements both IDisposable and IAsyncDisposable.
/// Tracks which dispose method was called.
/// </summary>
public class DualDisposableService : IDisposable, IAsyncDisposable
{
    public bool SyncDisposeCalled { get; private set; }
    public bool AsyncDisposeCalled { get; private set; }
    public Guid InstanceId { get; } = Guid.NewGuid();

    public void Dispose() => SyncDisposeCalled = true;

    public ValueTask DisposeAsync()
    {
        AsyncDisposeCalled = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// A service that implements only IAsyncDisposable (not IDisposable).
/// Used to test the sync dispose fallback path that calls DisposeAsync().AsTask().GetAwaiter().GetResult().
/// </summary>
public class AsyncOnlyDisposableService : IAsyncDisposable
{
    public bool IsDisposed { get; private set; }
    public Guid InstanceId { get; } = Guid.NewGuid();

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }
}

#endregion
