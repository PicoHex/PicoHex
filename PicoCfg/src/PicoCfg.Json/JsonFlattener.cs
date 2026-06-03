using System.Text;
using PicoJetson;
using PicoSerDe.Core;

namespace PicoCfg.Json;

internal static class JsonFlattener
{
    /// <summary>
    /// Flattens a JSON document into a flat dictionary using ':' as the path separator.
    /// Nested objects become compound keys: {"A":{"B":"v"}} → {"A:B":"v"}.
    /// Arrays are skipped — their values are not flattened into the dictionary.
    /// </summary>
    public static Dictionary<string, string> Flatten(ReadOnlySpan<byte> json)
    {
        var result = new Dictionary<string, string>();
        var reader = new JsonReader(json);
        var path = new List<string>();

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case TokenType.PropertyName:
                    path.Add(Encoding.UTF8.GetString(reader.ValueSpan));
                    break;

                case TokenType.String:
                case TokenType.Int32:
                case TokenType.Int64:
                case TokenType.Float32:
                case TokenType.Float64:
                case TokenType.Bool:
                    if (path.Count > 0)
                    {
                        var key = string.Join(":", path);
                        var value = TokenValueToString(reader);
                        result[key] = value;
                        path.RemoveAt(path.Count - 1);
                    }
                    break;

                case TokenType.Null:
                    if (path.Count > 0)
                        path.RemoveAt(path.Count - 1);
                    break;

                case TokenType.ArrayStart:
                    if (path.Count > 0)
                        path.RemoveAt(path.Count - 1);
                    SkipArray(ref reader);
                    break;
            }
        }

        return result;
    }

    private static void SkipArray(ref JsonReader reader)
    {
        var depth = 1;
        while (depth > 0 && reader.Read())
        {
            switch (reader.TokenType)
            {
                case TokenType.ArrayStart:
                    depth++;
                    break;
                case TokenType.ArrayEnd:
                    depth--;
                    break;
            }
        }
    }

    private static string TokenValueToString(JsonReader reader)
    {
        return reader.TokenType switch
        {
            TokenType.String
            or TokenType.Int32
            or TokenType.Int64
            or TokenType.Float32
            or TokenType.Float64
            or TokenType.Bool
                => Encoding.UTF8.GetString(reader.ValueSpan),
            _ => ""
        };
    }
}
