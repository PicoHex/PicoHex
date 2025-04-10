namespace PicoHex.DI.Abstractions;

public interface ISvcResolver : IDisposable, IAsyncDisposable
{
    object Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    );
}

public static class SvcResolverExtensions
{
    public static T Resolve<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T
    >(this ISvcResolver provider) => (T)provider.Resolve(typeof(T));
}
