namespace PicoHex.DI.Abstractions;

public interface ISvcResolver
{
    object? Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType
    );
}

public static class SvcResolverExtensions
{
    public static T? Resolve<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T
    >(this ISvcResolver provider)
    {
        var obj = provider.Resolve(typeof(T));
        return obj is null ? default : (T)obj;
    }
}
