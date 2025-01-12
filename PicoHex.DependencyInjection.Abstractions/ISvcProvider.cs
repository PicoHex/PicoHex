namespace PicoHex.DependencyInjection.Abstractions;

public interface ISvcProvider
{
    object? Resolve(Type serviceType);
    ISvcScope CreateScope();
}

public static class SvcProviderExtensions
{
    public static T Resolve<T>(this ISvcProvider provider)
    {
        return (T)provider.Resolve(typeof(T))!;
    }
}
