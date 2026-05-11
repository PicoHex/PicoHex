namespace PicoCfg.Abs;

/// <summary>
/// A live hierarchical section view over an <see cref="ICfg"/> that prepends a path prefix
/// to all key lookups. Nested sections compose: <c>GetSection("A").GetSection("B")</c>
/// produces lookups as <c>"A:B:&lt;key&gt;"</c>.
/// This is a live view — it always delegates to the parent and reflects any reloads
/// the parent might observe.
/// </summary>
internal sealed class CfgSection : ICfgSection
{
    private readonly ICfg _parent;
    private readonly string _path;

    internal CfgSection(ICfg parent, string path)
    {
        _parent = parent ?? throw new ArgumentNullException(nameof(parent));
        _path = path ?? string.Empty;
    }

    internal ICfg Parent => _parent;

    public string Path => _path;

    public bool TryGetValue(string key, out string? value)
    {
        return string.IsNullOrEmpty(_path)
            ? _parent.TryGetValue(key, out value)
            : _parent.TryGetValue(string.Concat(_path, ":", key), out value);
    }
}
