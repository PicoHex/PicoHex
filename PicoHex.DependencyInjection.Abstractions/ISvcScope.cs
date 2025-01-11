namespace PicoHex.DependencyInjection.Abstractions;

public interface ISvcScope : IDisposable, IAsyncDisposable
{
    object Resolve(Type implementationType);
}

public static class SvcScopeExtensions
{
    public static TService Resolve<TService>(this ISvcScope scope) =>
        (TService)scope.Resolve(typeof(TService));
}
