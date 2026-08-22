namespace PicoCfg;

/// <summary>
/// Binds and caches a configuration value at construction time.
/// Each <see cref="Value"/> access returns the same cached instance.
/// </summary>
/// <typeparam name="T">The bound configuration type.</typeparam>
/// <remarks>
/// Implementation detail of the options pattern — public only so PicoCfg.DI can
/// construct it without InternalsVisibleTo (same pattern as <c>CfgBindRuntime</c>).
/// </remarks>
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
public sealed class CfgOptions<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicConstructors
            | DynamicallyAccessedMemberTypes.PublicProperties
    )]
        T
> : ICfgOptions<T>
{
    private readonly T _value;

    /// <summary>
    /// Binds a new instance of <typeparamref name="T"/> from the given <paramref name="cfg"/>,
    /// optionally scoped to the specified <paramref name="section"/> prefix, and caches it.
    /// </summary>
    public CfgOptions(ICfg cfg, string? section = null)
    {
        _value = CfgBind.Bind<T>(cfg, section);
    }

    /// <inheritdoc />
    public T Value => _value;
}
