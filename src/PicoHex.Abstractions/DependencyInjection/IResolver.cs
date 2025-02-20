namespace PicoHex.Abstractions.DependencyInjection;

public interface IResolver
{
    object? Resolve(Type serviceType);
}

public static class SvcResolverExtensions
{
    public static T? Resolve<T>(this IResolver provider)
    {
        var obj = provider.Resolve(typeof(T));
        return obj is null ? default : (T)obj;
    }
}
