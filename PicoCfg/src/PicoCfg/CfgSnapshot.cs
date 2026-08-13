namespace PicoCfg;

internal sealed class CfgSnapshot : ICfgSnapshot
{
    public static CfgSnapshot Empty { get; } = new(new Dictionary<string, string>(), 0);

    internal CfgSnapshot(IReadOnlyDictionary<string, string> values)
        : this(values, ConfigDataComparer.ComputeFingerprint(values)) { }

    internal CfgSnapshot(IReadOnlyDictionary<string, string> values, int fingerprint)
    {
        Values = values;
        Fingerprint = fingerprint;
    }

    internal IReadOnlyDictionary<string, string> Values { get; }
    internal int Fingerprint { get; }

    // Lazily built case-insensitive index for the fallback path. Snapshots
    // are immutable and read concurrently, so the index is built at most once
    // via LazyInitializer. First-inserted-wins preserves the enumeration
    // order semantics of the previous O(n) fallback for keys that differ
    // only by case.
    private Dictionary<string, string>? _caseInsensitiveIndex;

    public IReadOnlyDictionary<string, string> GetAllValues() => Values;

    public bool TryGetValue(string path, out string? value)
    {
        if (Values.TryGetValue(path, out var existingValue))
        {
            value = existingValue;
            return true;
        }

        // Case-insensitive fallback so JSON camelCase / YAML / INI / TOML keys
        // match PascalCase C# property names during binding. Indexed after the
        // first miss so snapshots that never miss never pay the build cost.
        var index = LazyInitializer.EnsureInitialized(
            ref _caseInsensitiveIndex,
            () => BuildCaseInsensitiveIndex(this)
        );

        if (index.TryGetValue(path, out value))
            return true;

        value = null;
        return false;
    }

    private static Dictionary<string, string> BuildCaseInsensitiveIndex(CfgSnapshot snapshot)
    {
        var values = snapshot.Values;
        var index = new Dictionary<string, string>(values.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in values)
            index.TryAdd(key, value);
        return index;
    }
}
