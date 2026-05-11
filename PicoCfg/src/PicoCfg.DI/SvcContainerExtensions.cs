namespace PicoCfg.DI;

/// <summary>
/// PicoDI registration helpers for PicoCfg configuration roots and source-generated bound POCOs.
/// Call <see cref="RegisterCfgRoot"/> first, then use <c>RegisterCfg*</c> to expose typed configuration views.
/// </summary>
public static class SvcContainerExtensions
{
    extension(ISvcContainer container)
    {
        /// <summary>
        /// Registers the configuration root as <see cref="ICfgRoot"/> and <see cref="ICfg"/>.
        /// Must be called before <c>RegisterCfg*</c> methods that consume configuration.
        /// </summary>
        public ISvcContainer RegisterCfgRoot(ICfgRoot root)
        {
            ArgumentNullException.ThrowIfNull(root);
            return container
                .RegisterSingleton<ICfgRoot>(_ => root)
                .RegisterSingleton<ICfg>(_ => root);
        }

        /// <summary>
        /// Registers a transient bound POCO of type <typeparamref name="T"/> resolved from the configuration root.
        /// Requires <c>RegisterCfgRoot</c> to have been called first.
        /// </summary>
        public ISvcContainer RegisterCfgTransient<T>(string? section = null)
            where T : class
            => container.RegisterTransient<T>(scope => Bind<T>(scope, section));

        /// <summary>
        /// Registers a scoped bound POCO of type <typeparamref name="T"/> resolved from the configuration root.
        /// Requires <c>RegisterCfgRoot</c> to have been called first.
        /// </summary>
        public ISvcContainer RegisterCfgScoped<T>(string? section = null)
            where T : class
            => container.RegisterScoped<T>(scope => Bind<T>(scope, section));

        /// <summary>
        /// Registers a singleton bound POCO of type <typeparamref name="T"/> resolved from the configuration root.
        /// Requires <c>RegisterCfgRoot</c> to have been called first.
        /// </summary>
        public ISvcContainer RegisterCfgSingleton<T>(string? section = null)
            where T : class
            => container.RegisterSingleton<T>(scope => Bind<T>(scope, section));
    }

    private static T Bind<T>(ISvcScope scope, string? section)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(scope);

        var root = TryGetServices<ICfgRoot>(scope).LastOrDefault();
        if (root is not null)
            return CfgBind.Bind<T>(root, section);

        var cfg = TryGetServices<ICfg>(scope).LastOrDefault();
        if (cfg is not null)
            return CfgBind.Bind<T>(cfg, section);

        throw new InvalidOperationException(
            "No PicoCfg configuration source is registered. Call RegisterCfgRoot(...) before registering bound configuration services."
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
