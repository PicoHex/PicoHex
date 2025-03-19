namespace PicoHex.IoC;

public class SvcProvider(ISvcContainer container, ISvcScopeFactory scopeFactory) : ISvcProvider
{
    private readonly ConcurrentStack<Type> _resolving = new();

    public object Resolve(Type serviceType)
    {
        var svcDescriptor = container.GetDescriptor(serviceType);
        if (svcDescriptor is null)
            throw new InvalidOperationException($"Type {serviceType.Name} is not registered.");

        // 循环依赖检测
        if (_resolving.Contains(serviceType))
        {
            var cycle = string.Join(" → ", _resolving.Reverse().Select(t => t.Name));
            throw new InvalidOperationException(
                $"Circular dependency detected: {cycle} → {serviceType.Name}"
            );
        }

        _resolving.Push(serviceType);
        try
        {
            return svcDescriptor.Factory!(this);
        }
        finally
        {
            _resolving.TryPop(out _);
        }
    }

    public ISvcScope CreateScope() => scopeFactory.CreateScope(this);
}
