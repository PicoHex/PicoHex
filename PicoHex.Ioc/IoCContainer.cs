namespace PicoHex.Ioc;

public class DiContainer
{
    private readonly ConcurrentDictionary<Type, Registration> _registrations = new();

    private readonly AsyncLocal<Guid> _scopeId = new();

    internal void SetScope(Guid scopeId) => _scopeId.Value = scopeId;

    internal void ClearScope() => _scopeId.Value = Guid.Empty;

    public DiScope CreateScope()
    {
        return new DiScope(this);
    }

    public void Register<TService, TImpl>(Lifetime lifetime = Lifetime.Transient)
        where TImpl : TService
    {
        _registrations[typeof(TService)] = new Registration
        {
            Lifetime = lifetime,
            Factory = CreateFactory(typeof(TImpl))
        };
    }

    public TService Resolve<TService>()
    {
        return (TService)Resolve(typeof(TService), new Stack<Type>());
    }

    private object Resolve(Type serviceType, Stack<Type> stack)
    {
        if (!_registrations.TryGetValue(serviceType, out var registration))
            throw new InvalidOperationException($"Type {serviceType.Name} not registered.");

        if (stack.Contains(serviceType))
            throw new InvalidOperationException(
                $"Cyclic dependency detected for {serviceType.Name}."
            );

        stack.Push(serviceType);
        var result = GetInstance(registration, stack);
        stack.Pop();

        return result;
    }

    private object GetInstance(Registration registration, Stack<Type> stack)
    {
        switch (registration.Lifetime)
        {
            case Lifetime.Singleton:
                return registration.SingletonInstance
                    ?? (registration.SingletonInstance = registration.Factory(this));
            case Lifetime.PerThread:
                if (registration.ThreadInstance.Value == null)
                    registration.ThreadInstance.Value = registration.Factory(this);
                return registration.ThreadInstance.Value;
            case Lifetime.Scoped:
                if (_scopeId.Value == Guid.Empty)
                    return registration.Factory(this);
                return registration
                    .ScopedInstances
                    .GetOrAdd(_scopeId.Value, _ => registration.Factory(this));
            default:
                return registration.Factory(this);
        }
    }

    private Func<DiContainer, object> CreateFactory(Type implType)
    {
        var ctor = implType
            .GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var containerParam = Expression.Parameter(typeof(DiContainer), "container");
        var args = ctor.GetParameters();
        var argExpressions = new Expression[args.Length];

        for (int i = 0; i < args.Length; i++)
        {
            var method = typeof(DiContainer)
                .GetMethod(nameof(Resolve), new Type[0])
                .MakeGenericMethod(args[i].ParameterType);

            argExpressions[i] = Expression.Call(containerParam, method);
        }

        var newExpr = Expression.New(ctor, argExpressions);
        var lambda = Expression.Lambda<Func<DiContainer, object>>(newExpr, containerParam);
        return lambda.Compile();
    }
}
