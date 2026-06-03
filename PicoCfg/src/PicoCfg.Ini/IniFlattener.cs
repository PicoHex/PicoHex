using System.Text;

namespace PicoCfg.Ini;

internal static class IniFlattener
{
    public static Dictionary<string, string> Flatten(ReadOnlySpan<byte> ini)
    {
        var result = new Dictionary<string, string>();
        var text = Encoding.UTF8.GetString(ini);
        string? currentSection = null;

        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#'))
                continue;

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentSection = trimmed[1..^1].Trim().Replace('.', ':');
                continue;
            }

            var eq = trimmed.IndexOf('=');
            if (eq < 0)
                continue;

            var key = trimmed[..eq].Trim();
            var value = trimmed[(eq + 1)..].Trim();

            var fullKey = currentSection is not null ? $"{currentSection}:{key}" : key;
            result[fullKey] = value;
        }

        return result;
    }
}
