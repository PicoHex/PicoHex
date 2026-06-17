namespace PicoCfg;

internal static class ConfigDataComparer
{
    public static int ComputeFingerprint(IEnumerable<KeyValuePair<string, string>> values)
    {
        var fingerprint = 0;

        foreach (var (key, value) in values)
        {
            fingerprint ^= Mix(key, value);
        }

        return fingerprint;
    }

    public static bool Equals(
        CfgSnapshot left,
        IReadOnlyDictionary<string, string> right,
        int rightFingerprint
    )
    {
        if (left.Values.Count != right.Count)
            return false;

        if (left.Fingerprint != rightFingerprint)
            return false;

        return Equals(left.Values, right);
    }

    public static bool Equals(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right
    )
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left.Count != right.Count)
            return false;

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var otherValue))
                return false;

            if (!string.Equals(value, otherValue, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static int Mix(string key, string value)
    {
        var hash = new HashCode();
        hash.Add(key, StringComparer.Ordinal);
        hash.Add(value, StringComparer.Ordinal);
        return hash.ToHashCode();
    }
}
