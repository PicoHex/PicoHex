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

    internal IReadOnlyDictionary<string, string> GetAllValues() => Values;

    public bool TryGetValue(string path, out string? value)
    {
        if (Values.TryGetValue(path, out var existingValue))
        {
            value = existingValue;
            return true;
        }

        value = null;
        return false;
    }
}
