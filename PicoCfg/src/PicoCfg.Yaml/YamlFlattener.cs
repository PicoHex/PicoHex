using System.Text;

namespace PicoCfg.Yaml;

internal static class YamlFlattener
{
    public static Dictionary<string, string> Flatten(ReadOnlySpan<byte> yaml)
    {
        var result = new Dictionary<string, string>();
        var text = Encoding.UTF8.GetString(yaml);
        var lines = text.Split('\n');
        var path = new List<string>();

        foreach (var line in lines)
        {
            if (line.Trim().Length == 0 || line.Trim().StartsWith('#'))
                continue;

            var indent = line.Length - line.TrimStart().Length;
            var content = line.Trim();

            // Adjust path depth based on indentation
            while (path.Count > 0 && (path.Count * 2) > indent)
                path.RemoveAt(path.Count - 1);

            var colon = content.IndexOf(':');
            if (colon < 0)
                continue;

            var key = content[..colon].Trim();
            var value = content[(colon + 1)..].Trim();

            // Update path at current depth
            if (path.Count >= indent / 2 + 1)
                path.RemoveAt(path.Count - 1);
            path.Add(key);

            if (value.Length > 0)
            {
                var unquoted = Unquote(value);
                var fullKey = string.Join(":", path);
                result[fullKey] = unquoted;
                path.RemoveAt(path.Count - 1);
            }
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
