namespace PicoHex.DependencyInjection.Abstractions;

public interface IResolver
{
    object? Resolve(Type serviceType);
}

public static class SvcResolverExtensions
{
    public static T Resolve<T>(this IResolver provider)
    {
        return (T)provider.Resolve(typeof(T))!;
    }
}
