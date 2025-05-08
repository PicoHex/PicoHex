namespace Pico.Json;

internal static class JsonStringHelper
{
    public static string Escape(string? value)
    {
        if (value is null)
            return "";
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\b", "\\b")
            .Replace("\f", "\\f")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
