namespace PicoCfg.Abs;

/// <summary>
/// Represents an exact-lookup configuration view.
/// This is the minimal consumer-facing contract for reading configuration values.
/// </summary>
public interface ICfg
{
    bool TryGetValue(string path, out string? value);
}
