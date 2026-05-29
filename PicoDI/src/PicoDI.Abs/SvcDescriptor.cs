namespace PicoDI.Abs;

/// <summary>
/// Describes a service registration including its service type, factory, and lifetime.
/// For AOT compatibility, services should be registered with factory delegates
/// generated at compile time by PicoDI.Gen source generator.
/// </summary>
/// <remarks>
/// <para><b>Which constructor should I use?</b></para>
/// <list type="table">
///   <item>
///     <term><c>SvcDescriptor.Create(serviceType, factory)</c></term>
///     <description><b>Canonical.</b> Register a service with a factory delegate.</description>
///   </item>
///   <item>
///     <term><c>SvcDescriptor.FromInstance(serviceType, instance)</c></term>
///     <description>Register a pre-existing singleton instance.</description>
///   </item>
///   <item>
///     <term><c>new SvcDescriptor(serviceType, factory, lifetime)</c></term>
///     <description>Alternative to <c>Create</c> when you prefer constructor syntax.</description>
///   </item>
///   <item>
///     <term><c>new SvcDescriptor(serviceType, implType, lifetime)</c></term>
///     <description>Type-based registration (primarily used by the source generator and open-generic registrations).</description>
///   </item>
/// </list>
/// </remarks>
/// <param name="serviceType">The service type (interface or base class) being registered.</param>
/// <param name="implementationType">The concrete implementation type (optional, for open generics).</param>
/// <param name="lifetime">The service lifetime (Transient, Scoped, or Singleton).</param>
public sealed class SvcDescriptor(
    Type serviceType,
    Type? implementationType,
    SvcLifetime lifetime = SvcLifetime.Singleton
)
{
    /// <summary>
    /// Gets the service type (interface or base class) being registered.
    /// </summary>
    public Type ServiceType { get; } =
        serviceType ?? throw new ArgumentNullException(nameof(serviceType));

    /// <summary>
    /// Gets the concrete implementation type.
    /// Used primarily for open generic registrations to track the implementation type.
    /// </summary>
    public Type ImplementationType { get; } = implementationType ?? serviceType;

    /// <summary>
    /// Gets or sets the pre-existing singleton instance for this service.
    /// Set internally when constructing a descriptor with an existing instance
    /// via <see cref="FromInstance"/>. Only for use by the PicoDI runtime —
    /// not intended for consumer code. No concurrent access — all descriptor
    /// writes happen during registration under the container's registration lock.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public object? SingleInstance { get; internal set; }

    /// <summary>
    /// Gets the factory function used to create service instances.
    /// </summary>
    public Func<ISvcScope, object>? Factory { get; }

    /// <summary>
    /// Gets the generated factory identifier for SG-registered services.
    /// Negative values indicate runtime-registered services that use the Factory delegate.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public int GeneratedFactoryId { get; internal set; } = -1;

    /// <summary>
    /// Gets the service lifetime.
    /// </summary>
    public SvcLifetime Lifetime { get; } = lifetime;

    /// <summary>
    /// Creates a service descriptor for a pre-existing singleton instance.
    /// </summary>
    public static SvcDescriptor FromInstance(Type serviceType, object instance)
    {
        if (serviceType is null)
            throw new ArgumentNullException(nameof(serviceType));
        if (instance is null)
            throw new ArgumentNullException(nameof(instance));
        var descriptor = new SvcDescriptor(serviceType, serviceType);
        descriptor.SingleInstance = instance;
        return descriptor;
    }

    /// <summary>
    /// Creates a service descriptor with a factory delegate.
    /// This is the canonical factory method.
    /// </summary>
    public static SvcDescriptor Create(
        Type serviceType,
        Func<ISvcScope, object> factory,
        SvcLifetime lifetime = SvcLifetime.Singleton
    )
    {
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));
        return new SvcDescriptor(serviceType, factory, lifetime);
    }

    /// <summary>
    /// Creates a typed service descriptor with a strongly-typed factory delegate.
    /// </summary>
    public static SvcDescriptor Create<T>(
        Func<ISvcScope, T> factory,
        SvcLifetime lifetime = SvcLifetime.Singleton
    )
        where T : class
    {
        if (factory is null)
            throw new ArgumentNullException(nameof(factory));
        return new SvcDescriptor(typeof(T), s => factory(s)!, lifetime);
    }

    internal SvcDescriptor(Type serviceType, Func<ISvcScope, object> factory, SvcLifetime lifetime)
        : this(serviceType, serviceType, lifetime) =>
        Factory = factory ?? throw new ArgumentNullException(nameof(factory));
}
