namespace PicoHex.IoC;

public class SvcProvider(ISvcContainer container, ISvcScopeFactory scopeFactory) : ISvcProvider
{
    private readonly ConcurrentStack<Type> _resolving = new();

    public object Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    )
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
            return svcDescriptor.Lifetime switch
            {
                SvcLifetime.Transient => svcDescriptor.Factory!(this),
                SvcLifetime.Singleton => svcDescriptor.Factory!(this),
                SvcLifetime.Scoped => svcDescriptor.Factory!(this),
                SvcLifetime.PerThread => svcDescriptor.Factory!(this),
                SvcLifetime.Pooled => svcDescriptor.Factory!(this),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        finally
        {
            _resolving.TryPop(out _);
        }
    }

    public ISvcScope CreateScope() => scopeFactory.CreateScope(this);
}
