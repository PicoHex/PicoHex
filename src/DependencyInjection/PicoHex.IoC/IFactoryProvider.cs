namespace PicoHex.IoC;

/// <summary>
/// Interface for factory providers that create service instances
/// </summary>
public interface IFactoryProvider
{
    /// <summary>
    /// Registers factories with the container
    /// </summary>
    /// <param name="container">The service container</param>
    /// <param name="factories">Dictionary of factories to populate</param>
    void RegisterFactories(
        ServiceContainer container,
        Dictionary<Type, Func<ServiceContainer, object>> factories
    );
}
