namespace PicoHex.IoC.Abstractions;

public interface ISvcResolver
{
    object? Resolve(Type serviceType);
}

public static class SvcResolverExtensions
{
    public static T? Resolve<T>(this ISvcResolver provider)
    {
        var obj = provider.Resolve(typeof(T));
        return obj is null ? default : (T)obj;
    }
}
