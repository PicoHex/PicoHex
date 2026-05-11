namespace PicoCfg.Abs;

/// <summary>
/// Extension methods for creating hierarchical section views over <see cref="ICfg"/>.
/// </summary>
public static class CfgSectionExtensions
{
    /// <summary>
    /// Returns a live scoped section view that prepends <paramref name="path"/> to all key lookups.
    /// The returned view delegates to the original <see cref="ICfg"/> instance and reflects any
    /// reloads the parent might observe.
    /// </summary>
    /// <param name="cfg">The configuration instance to scope.</param>
    /// <param name="path">The section path prefix. An empty or <see langword="null"/> value
    /// returns an identity view that passes lookups through unchanged.</param>
    /// <returns>A live delegated <see cref="ICfg"/> scoped to the given section.</returns>
    public static ICfg GetSection(this ICfg cfg, string? path)
    {
        return cfg is null
            ? throw new ArgumentNullException(nameof(cfg))
            : new CfgSection(cfg, path ?? string.Empty);
    }
}
