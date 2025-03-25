namespace PicoHex.DI;

public sealed class SvcProvider(ISvcContainer container, ISvcScopeFactory scopeFactory)
    : ISvcProvider
{
    private static readonly AsyncLocal<ResolutionContext?> AsyncContext = new();

    public object Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    )
    {
        var context = AsyncContext.Value ??= new ResolutionContext();

        if (!context.TryEnterResolution(serviceType, out var cyclePath))
            throw new InvalidOperationException($"Circular dependency detected: {cyclePath}");

        try
        {
            var svcDescriptor = container.GetDescriptor(serviceType);
            if (svcDescriptor is null)
                throw new InvalidOperationException($"Type {serviceType.Name} is not registered.");

            return svcDescriptor.Lifetime switch
            {
                SvcLifetime.Transient => svcDescriptor.Factory!(this),
                SvcLifetime.Singleton => GetSingleton(svcDescriptor),
                SvcLifetime.Scoped => svcDescriptor.Factory!(this),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        finally
        {
            context.ExitResolution();
            if (context.IsEmpty)
                AsyncContext.Value = null;
        }
    }

    private object GetSingleton(SvcDescriptor svcDescriptor)
    {
        if (svcDescriptor.SingleInstance is not null)
            return svcDescriptor.SingleInstance;
        lock (svcDescriptor)
            return svcDescriptor.SingleInstance ??= svcDescriptor.Factory!(this);
    }

    public ISvcScope CreateScope() => scopeFactory.CreateScope(this);
}
