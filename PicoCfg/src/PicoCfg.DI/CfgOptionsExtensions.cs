namespace PicoCfg.DI;

/// <summary>
/// PicoDI registration helpers for <see cref="ICfgOptions{T}"/>.
/// Requires <c>RegisterCfgRoot</c> to have been called first.
/// </summary>
public static class CfgOptionsExtensions
{
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Registers a singleton <see cref="ICfgOptions{T}"/> that binds and caches the configuration value once.
        /// Each resolution returns the same cached instance.
        /// Requires <c>RegisterCfgRoot</c> to have been called first.
        /// </summary>
        public ISvcContainer RegisterCfgOptionsSingleton<T>(string? section = null)
            where T : class
            => container.RegisterSingleton<ICfgOptions<T>>(scope => new CfgOptions<T>(Resolve(scope), section));

        /// <summary>
        /// Registers a scoped <see cref="ICfgOptions{T}"/> that rebinds the configuration value on every resolution.
        /// Each resolution creates a new instance from the current configuration state.
        /// Requires <c>RegisterCfgRoot</c> to have been called first.
        /// </summary>
        public ISvcContainer RegisterCfgOptionsScoped<T>(string? section = null)
            where T : class
            => container.RegisterScoped<ICfgOptions<T>>(scope => new CfgOptionsSnapshot<T>(Resolve(scope), section));
    }

    private static ICfg Resolve(ISvcScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var root = TryGetServices<ICfgRoot>(scope).LastOrDefault();
        if (root is not null)
            return root;

        var cfg = TryGetServices<ICfg>(scope).LastOrDefault();
        if (cfg is not null)
            return cfg;

        throw new InvalidOperationException(
            "No PicoCfg configuration source is registered. Call RegisterCfgRoot(...) before registering configuration options."
        );
    }

    private static IEnumerable<T> TryGetServices<T>(ISvcScope scope) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(scope);

        try
        {
            return scope.GetServices<T>();
        }
        catch (PicoDiException)
        {
            // PicoDI.Abs does not expose an IsRegistered<T>() query API.
            // Catching PicoDiException from GetServices<T>() is the only non-throwing
            // path to detect unregistered service types at this layer.
            return [];
        }
    }
}
