namespace PicoHex.IoC;

/// <summary>
/// Interface for resolving services
/// </summary>
public interface IServiceProvider
{
    /// <summary>
    /// Gets a service of the specified type
    /// </summary>
    /// <typeparam name="T">The service type</typeparam>
    /// <returns>The service instance</returns>
    T GetService<T>()
        where T : class;

    /// <summary>
    /// Gets a service of the specified type
    /// </summary>
    /// <param name="serviceType">The service type</param>
    /// <returns>The service instance</returns>
    object GetService(Type serviceType);
}
