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
        // Fast path: exact match
        var fullKey = string.IsNullOrEmpty(_path) ? key : string.Concat(_path, ":", key);
        if (_parent.TryGetValue(fullKey, out value))
            return true;

        // Case-insensitive fallback for parents that lack the built-in
        // fallback in CfgSnapshot / CfgSnapshotComposer.  Enumerate
        // all parent keys and compare relative keys within this section.
        IReadOnlyDictionary<string, string>? parentAll = TryGetAll(_parent);
        if (parentAll is { Count: > 0 })
        {
            var searchPrefix = string.IsNullOrEmpty(_path) ? "" : (_path + ":");
            foreach (var kvp in parentAll)
            {
                if (kvp.Key.StartsWith(searchPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    var relativeKey = kvp.Key.Substring(searchPrefix.Length);
                    if (string.Equals(relativeKey, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = kvp.Value;
                        return true;
                    }
                }
            }
        }

        value = null;
        return false;
    }

    private static IReadOnlyDictionary<string, string>? TryGetAll(ICfg cfg)
    {
        if (cfg is ICfgSnapshot s)
            return s.GetAllValues();
        if (cfg is CfgSection cs)
        {
            // Walk up the section chain to find the root enumerable
            var rootAll = TryGetAll(cs._parent);
            if (rootAll is null or { Count: 0 })
                return rootAll;

            var searchPrefix = string.IsNullOrEmpty(cs._path) ? "" : (cs._path + ":");
            var filtered = new Dictionary<string, string>(rootAll.Count);
            foreach (var kvp in rootAll)
            {
                if (kvp.Key.StartsWith(searchPrefix, StringComparison.OrdinalIgnoreCase))
                    filtered[kvp.Key.Substring(searchPrefix.Length)] = kvp.Value;
            }
            return filtered;
        }
        return null;
    }
}
