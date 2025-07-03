namespace Pico.DI.Abs;

public interface ISvcResolver
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
    >(this ISvcResolver resolver) => (T)resolver.Resolve(typeof(T));
}
