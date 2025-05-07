namespace Pico.Json;

public static partial class PicoJsonSerializer
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining
    )]
    public static string Serialize<T>(T value)
    {
        var sb = new System.Text.StringBuilder();
        Serialize(sb, value);
        return sb.ToString();
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining
    )]
    public static void Serialize<T>(System.Text.StringBuilder sb, T value)
    {
        if (value == null)
        {
            sb.Append("null");
            return;
        }

        switch (value)
        {
            case int i:
                JsonWriter.WriteNumber(sb, i);
                break;
            case string s:
                JsonWriter.WriteString(sb, s);
                break;
            case bool b:
                JsonWriter.WriteBool(sb, b);
                break;
            default:
                Generated.PicoJsonSerializer.Serialize(sb, value);
                break;
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining
    )]
    public static T Deserialize<T>(string json) => Deserialize<T>(json.AsSpan());

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining
    )]
    public static T Deserialize<T>(System.ReadOnlySpan<char> json)
    {
        int index = 0;
        return Deserialize<T>(json, ref index);
    }

    internal static T Deserialize<T>(ReadOnlySpan<char> json, ref int index)
    {
        return typeof(T).Name switch
        {
            nameof(Int32) => (T)(object)JsonParser.ParseInt(json, ref index),
            nameof(String) => (T)(object)JsonParser.ParseStringSpan(json, ref index).ToString(),
            nameof(Boolean) => (T)(object)JsonParser.ParseBool(json, ref index),
            _ => Generated.PicoJsonSerializer.Deserialize<T>(json, ref index)
        };
    }
}
