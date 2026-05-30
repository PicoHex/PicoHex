namespace PicoAop.Benchmarks;

#region Method-signature service interfaces (×4 depths each)

// void Do()
public interface IVoidSvc_D0
{
    void Do();
}

public interface IVoidSvc_D1
{
    void Do();
}

public interface IVoidSvc_D3
{
    void Do();
}

public interface IVoidSvc_D5
{
    void Do();
}

// T Get()
public interface IReturnSvc_D0
{
    int Get();
}

public interface IReturnSvc_D1
{
    int Get();
}

public interface IReturnSvc_D3
{
    int Get();
}

public interface IReturnSvc_D5
{
    int Get();
}

// ValueTask DoAsync()
public interface ITaskVoidSvc_D0
{
    ValueTask DoAsync();
}

public interface ITaskVoidSvc_D1
{
    ValueTask DoAsync();
}

public interface ITaskVoidSvc_D3
{
    ValueTask DoAsync();
}

public interface ITaskVoidSvc_D5
{
    ValueTask DoAsync();
}

// ValueTask<T> GetAsync()
public interface ITaskReturnSvc_D0
{
    ValueTask<int> GetAsync();
}

public interface ITaskReturnSvc_D1
{
    ValueTask<int> GetAsync();
}

public interface ITaskReturnSvc_D3
{
    ValueTask<int> GetAsync();
}

public interface ITaskReturnSvc_D5
{
    ValueTask<int> GetAsync();
}

#endregion

#region Concrete implementations (one class per signature, implements all 4 depths)

public sealed class VoidSvc : IVoidSvc_D0, IVoidSvc_D1, IVoidSvc_D3, IVoidSvc_D5
{
    public void Do() { }
}

public sealed class ReturnSvc : IReturnSvc_D0, IReturnSvc_D1, IReturnSvc_D3, IReturnSvc_D5
{
    public int Get() => 42;
}

public sealed class TaskVoidSvc : ITaskVoidSvc_D0, ITaskVoidSvc_D1, ITaskVoidSvc_D3, ITaskVoidSvc_D5
{
    public ValueTask DoAsync() => ValueTask.CompletedTask;
}

public sealed class TaskReturnSvc
    : ITaskReturnSvc_D0,
        ITaskReturnSvc_D1,
        ITaskReturnSvc_D3,
        ITaskReturnSvc_D5
{
    public ValueTask<int> GetAsync() => ValueTask.FromResult(42);
}

#endregion

#region No-op interceptors (5 identical types, for depth 1-5 chains)

public sealed class N1 : InterceptorBase { }

public sealed class N2 : InterceptorBase { }

public sealed class N3 : InterceptorBase { }

public sealed class N4 : InterceptorBase { }

public sealed class N5 : InterceptorBase { }

#endregion

#region Container factory

public static class ContainerFactory
{
    /// <summary>
    /// Creates a container with all services and interceptors registered for all depths.
    /// The PicoAop.Gen source generator scans the InterceptBy chains here
    /// and emits decorator classes + DI registrations via ModuleInitializer.
    /// </summary>
    public static SvcContainer Create()
    {
        var container = new SvcContainer();

        // Register all no-op interceptors as singletons
        container.RegisterSingleton<N1>();
        container.RegisterSingleton<N2>();
        container.RegisterSingleton<N3>();
        container.RegisterSingleton<N4>();
        container.RegisterSingleton<N5>();

        // Register concrete implementations as themselves (required by PicoAop.Gen
        // decorator chain resolution — the innermost decorator resolves the concrete
        // implementation type directly).
        container.Register<VoidSvc>(SvcLifetime.Scoped);
        container.Register<ReturnSvc>(SvcLifetime.Scoped);
        container.Register<TaskVoidSvc>(SvcLifetime.Scoped);
        container.Register<TaskReturnSvc>(SvcLifetime.Scoped);

        // — void Do() —
        container.Register<IVoidSvc_D0, VoidSvc>(SvcLifetime.Scoped);
        container.Register<IVoidSvc_D1, VoidSvc>(SvcLifetime.Scoped).InterceptBy<N1>();
        container
            .Register<IVoidSvc_D3, VoidSvc>(SvcLifetime.Scoped)
            .InterceptBy<N1>()
            .InterceptBy<N2>()
            .InterceptBy<N3>();
        container
            .Register<IVoidSvc_D5, VoidSvc>(SvcLifetime.Scoped)
            .InterceptBy<N1>()
            .InterceptBy<N2>()
            .InterceptBy<N3>()
            .InterceptBy<N4>()
            .InterceptBy<N5>();

        // — T Get() —
        container.Register<IReturnSvc_D0, ReturnSvc>(SvcLifetime.Scoped);
        container.Register<IReturnSvc_D1, ReturnSvc>(SvcLifetime.Scoped).InterceptBy<N1>();
        container
            .Register<IReturnSvc_D3, ReturnSvc>(SvcLifetime.Scoped)
            .InterceptBy<N1>()
            .InterceptBy<N2>()
            .InterceptBy<N3>();
        container
            .Register<IReturnSvc_D5, ReturnSvc>(SvcLifetime.Scoped)
            .InterceptBy<N1>()
            .InterceptBy<N2>()
            .InterceptBy<N3>()
            .InterceptBy<N4>()
            .InterceptBy<N5>();

        // — ValueTask DoAsync() —
        container.Register<ITaskVoidSvc_D0, TaskVoidSvc>(SvcLifetime.Scoped);
        container.Register<ITaskVoidSvc_D1, TaskVoidSvc>(SvcLifetime.Scoped).InterceptBy<N1>();
        container
            .Register<ITaskVoidSvc_D3, TaskVoidSvc>(SvcLifetime.Scoped)
            .InterceptBy<N1>()
            .InterceptBy<N2>()
            .InterceptBy<N3>();
        container
            .Register<ITaskVoidSvc_D5, TaskVoidSvc>(SvcLifetime.Scoped)
            .InterceptBy<N1>()
            .InterceptBy<N2>()
            .InterceptBy<N3>()
            .InterceptBy<N4>()
            .InterceptBy<N5>();

        // — ValueTask<T> GetAsync() —
        container.Register<ITaskReturnSvc_D0, TaskReturnSvc>(SvcLifetime.Scoped);
        container.Register<ITaskReturnSvc_D1, TaskReturnSvc>(SvcLifetime.Scoped).InterceptBy<N1>();
        container
            .Register<ITaskReturnSvc_D3, TaskReturnSvc>(SvcLifetime.Scoped)
            .InterceptBy<N1>()
            .InterceptBy<N2>()
            .InterceptBy<N3>();
        container
            .Register<ITaskReturnSvc_D5, TaskReturnSvc>(SvcLifetime.Scoped)
            .InterceptBy<N1>()
            .InterceptBy<N2>()
            .InterceptBy<N3>()
            .InterceptBy<N4>()
            .InterceptBy<N5>();

        container.Build();
        return container;
    }
}

#endregion
