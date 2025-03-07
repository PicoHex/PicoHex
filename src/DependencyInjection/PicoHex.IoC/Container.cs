namespace PicoHex.IoC;

public class Container
{
    private readonly Dictionary<Type, Func<Container, object>> _factories = new();
    private readonly HashSet<Type> _resolvingStack = new();

    public void Register<T>(Func<Container, T> factory)
    {
        _factories[typeof(T)] = c => factory(c)!;
    }

    public T Resolve<T>() => (T)Resolve(typeof(T));

    public object Resolve(Type type)
    {
        if (!_factories.TryGetValue(type, out var factory))
            throw new InvalidOperationException($"Type {type.Name} not registered");

        if (!_resolvingStack.Add(type))
            throw new CircularDependencyException($"Detected circular dependency on {type.Name}");

        try
        {
            return factory(this);
        }
        finally
        {
            _resolvingStack.Remove(type);
        }
    }
}
