namespace PicoCfg.Toml;

internal static class TomlFlattener
{
    public static Dictionary<string, string> Flatten(ReadOnlySpan<byte> toml)
    {
        var result = new Dictionary<string, string>();
        var text = Encoding.UTF8.GetString(toml);
        string? currentTable = null;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            // Table header: [Table] or [Parent.Child]
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentTable = trimmed[1..^1].Trim().Replace('.', ':');
                continue;
            }

            var eq = trimmed.IndexOf('=');
            if (eq < 0)
                continue;

            var key = trimmed[..eq].Trim();
            var value = trimmed[(eq + 1)..].Trim();
            // Unquote string values
            value = Unquote(value);

            var fullKey = currentTable is not null ? $"{currentTable}:{key}" : key;
            result[fullKey] = value;
        }

        return result;
    }

    private static string Unquote(string value)
    {
        if (
            value.Length >= 2
            && (
                (value.StartsWith('"') && value.EndsWith('"'))
                || (value.StartsWith('\'') && value.EndsWith('\''))
            )
        )
            return value[1..^1];
        return value;
    }
}
