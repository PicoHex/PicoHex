namespace PicoCfg;

/// <summary>
/// Rebinds a configuration value on every access.
/// Each <see cref="Value"/> call creates a new instance from the underlying configuration.
/// </summary>
/// <typeparam name="T">The bound configuration type.</typeparam>
internal sealed class CfgOptionsSnapshot<T> : ICfgOptions<T>
{
    private readonly ICfg _cfg;
    private readonly string? _section;

    /// <summary>
    /// Stores the configuration view and optional section prefix for rebinding on each access.
    /// </summary>
    public CfgOptionsSnapshot(ICfg cfg, string? section = null)
    {
        _cfg = cfg;
        _section = section;
    }

    /// <inheritdoc />
    public T Value => CfgBind.Bind<T>(_cfg, _section);
}
