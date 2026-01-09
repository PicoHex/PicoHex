namespace Pico.DI;

/// <summary>
/// Bootstrap class for AOT-friendly dependency injection
/// </summary>
public static class AotBootstrap
{
    /// <summary>
    /// Creates an AOT-friendly service container with compile-time optimizations
    /// </summary>
    public static ISvcContainer CreateAotContainer()
    {
        return new AotSvcContainer();
    }

    /// <summary>
    /// Creates a container optimized for the current environment
    /// (AOT container in AOT environments, regular container otherwise)
    /// </summary>
    public static ISvcContainer CreateOptimizedContainer()
    {
#if NET10_0_OR_GREATER
        // In .NET 10+, prefer AOT container for better performance
        return CreateAotContainer();
#else
        // Fallback to regular container for older frameworks
        return Bootstrap.CreateContainer();
#endif
    }

    /// <summary>
    /// Creates a container with compile-time service registrations
    /// </summary>
    public static ISvcContainer CreateCompileTimeContainer()
    {
        return new AotSvcContainer();
    }
}