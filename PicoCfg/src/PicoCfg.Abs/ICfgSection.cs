namespace PicoCfg.Abs;

/// <summary>
/// Represents a scoped hierarchical section view over an <see cref="ICfg"/>.
/// Each lookup is prefixed with <see cref="Path"/> followed by the separator,
/// forming a live delegation chain that reflects any reloads in the parent view.
/// </summary>
public interface ICfgSection : ICfg
{
    /// <summary>
    /// Gets the path prefix used for scoped key lookups.
    /// An empty string indicates an identity view that passes lookups through unchanged.
    /// </summary>
    string Path { get; }
}
