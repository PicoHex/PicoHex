namespace Pico.Json;

public static class JsonParser
{
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining
    )]
    public static void SkipWhitespace(System.ReadOnlySpan<char> json, ref int index)
    {
        while (index < json.Length && char.IsWhiteSpace(json[index]))
            index++;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining
    )]
    public static int ParseInt(System.ReadOnlySpan<char> json, ref int index)
    {
        int sign = 1,
            result = 0;
        if (json[index] == '-')
        {
            sign = -1;
            index++;
        }
        while (index < json.Length && char.IsDigit(json[index]))
            result = result * 10 + (json[index++] - '0');
        return sign * result;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining
    )]
    public static System.ReadOnlySpan<char> ParseStringSpan(
        System.ReadOnlySpan<char> json,
        ref int index
    )
    {
        if (json[index++] != '"')
            throw new System.FormatException("Expected '\"'");
        int start = index;
        while (index < json.Length && json[index] != '"')
            index++;
        var span = json.Slice(start, index - start);
        index++;
        return span;
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining
    )]
    public static bool ParseBool(System.ReadOnlySpan<char> json, ref int index)
    {
        if (json.Slice(index, 4).SequenceEqual("true".AsSpan()))
        {
            index += 4;
            return true;
        }
        if (json.Slice(index, 5).SequenceEqual("false".AsSpan()))
        {
            index += 5;
            return false;
        }
        throw new System.FormatException("Invalid boolean");
    }

    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining
    )]
    public static void SkipValue(System.ReadOnlySpan<char> json, ref int index)
    {
        int depth = 0;
        do
        {
            switch (json[index])
            {
                case '{':
                case '[':
                    depth++;
                    break;
                case '}':
                case ']':
                    depth--;
                    break;
            }
            index++;
        } while (depth > 0 && index < json.Length);
    }
}
