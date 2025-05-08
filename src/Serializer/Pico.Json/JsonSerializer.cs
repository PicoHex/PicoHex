using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Pico.Json;

public static partial class JsonSerializer
{
    private static bool TryGetValue(string json, string key, out string value)
    {
        var pattern = $"\"{key}\":(.*?)(,|}})";
        var match = Regex.Match(json, pattern);
        if (match.Success)
        {
            value = match.Groups[1].Value.Trim();
            return true;
        }
        value = null;
        return false;
    }

    public static void Serialize<T>(Stream stream, T value) =>
        SerializerCache<T>.Serialize(stream, value);

    public static T Deserialize<T>(Stream stream) => SerializerCache<T>.Deserialize(stream);

    private static partial class SerializerCache<T>
    {
        // 由源码生成器实现
    }
}
