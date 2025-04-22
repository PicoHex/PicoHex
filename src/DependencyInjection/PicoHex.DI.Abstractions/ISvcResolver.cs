namespace PicoHex.DI.Abstractions;

public interface ISvcResolver
{
    object Resolve(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.None)] Type serviceType
    );
}

public static class SvcResolverExtensions
{
    public static T Resolve<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.None)] T>(
        this ISvcResolver resolver
    ) => (T)resolver.Resolve(typeof(T));
}
