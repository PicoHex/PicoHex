namespace PicoCfg.Abs;

/// <summary>
/// Provides a typed configuration value.
/// </summary>
/// <typeparam name="T">The bound configuration type.</typeparam>
public interface ICfgOptions<out T>
{
    /// <summary>
    /// Gets the bound configuration value.
    /// </summary>
    T Value { get; }
}
