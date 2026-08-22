namespace PicoCfg.Json;

internal static class JsonFlattener
{
    /// <summary>
    /// Flattens a JSON document into a flat dictionary using ':' as the path separator.
    /// Nested objects become compound keys: {"A":{"B":"v"}} → {"A:B":"v"}.
    /// </summary>
    /// <param name="json">UTF-8 JSON bytes.</param>
    /// <param name="flattenArrays">
    /// When <see langword="false"/> (default), arrays are skipped — their values are
    /// not flattened into the dictionary. When <see langword="true"/>, array elements
    /// are indexed into the path: {"items":[1,2]} → items:0 / items:1,
    /// {"items":[{"a":1}]} → items:0:a, nested arrays extend the index chain
    /// ({"m":[[1,2]]} → m:0:0 / m:0:1). The indexed shape matches the list binding
    /// format used by CfgBind (Section:N and Section:N:Property).
    /// </param>
    public static Dictionary<string, string> Flatten(
        ReadOnlySpan<byte> json,
        bool flattenArrays = false
    )
    {
        var result = new Dictionary<string, string>();
        var reader = new JsonReader(json);
        var path = new List<string>();
        // For each open array: the path position of its index segment and the
        // current element index. Only tracked when flattenArrays is enabled.
        var arrays = new Stack<(int PathPosition, int Index)>();
        // For each open object: whether the object is a direct element of the
        // innermost array (its completion increments that array's index).
        var objectIsElement = new Stack<bool>();

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
                        PopOrAdvanceArray(path, arrays);
                    }
                    break;

                case TokenType.Null:
                    // null values are treated as missing keys, but inside an array
                    // they still occupy an element slot and advance the index.
                    if (path.Count > 0)
                        PopOrAdvanceArray(path, arrays);
                    break;

                case TokenType.ObjectStart:
                    objectIsElement.Push(
                        arrays.Count > 0 && path.Count == arrays.Peek().PathPosition + 1
                    );
                    break;

                case TokenType.ObjectEnd:
                    if (objectIsElement.Count > 0 && objectIsElement.Pop())
                    {
                        // The completed object was an array element — advance the
                        // innermost array's index (its path segment stays in place).
                        AdvanceInnermostArray(path, arrays);
                    }
                    else if (path.Count > 0)
                    {
                        // Pop the property name that opened this object
                        path.RemoveAt(path.Count - 1);
                    }
                    break;

                case TokenType.ArrayStart:
                    if (path.Count == 0)
                    {
                        // A root-level array has no key to attach to — skip it.
                        SkipArray(ref reader);
                        break;
                    }

                    if (!flattenArrays)
                    {
                        // Legacy behavior: arrays are skipped entirely.
                        path.RemoveAt(path.Count - 1);
                        SkipArray(ref reader);
                        break;
                    }

                    arrays.Push((path.Count, 0));
                    path.Add("0");
                    break;

                case TokenType.ArrayEnd:
                    if (arrays.Count == 0)
                        break;

                    var completed = arrays.Pop();
                    if (path.Count > 0)
                        path.RemoveAt(path.Count - 1); // pop the index segment

                    if (arrays.Count > 0)
                    {
                        if (completed.PathPosition == arrays.Peek().PathPosition + 1)
                        {
                            // The completed array was an element of the parent array
                            // (its index segment sat directly under the parent's) —
                            // the parent's index advances and the element slot stays.
                            AdvanceInnermostArray(path, arrays);
                        }
                        else if (path.Count > 0)
                        {
                            // The completed array was a property value — pop the
                            // property name that opened it.
                            path.RemoveAt(path.Count - 1);
                        }
                    }
                    else if (path.Count > 0)
                    {
                        // The array value completed — pop the property that opened it.
                        path.RemoveAt(path.Count - 1);
                    }
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// After a scalar (or null) value is consumed: if the value was a direct array
    /// element the innermost array index advances; otherwise the property name that
    /// held the value is popped from the path.
    /// </summary>
    private static void PopOrAdvanceArray(
        List<string> path,
        Stack<(int PathPosition, int Index)> arrays
    )
    {
        if (arrays.Count > 0 && path.Count - 1 == arrays.Peek().PathPosition)
        {
            AdvanceInnermostArray(path, arrays);
        }
        else
        {
            path.RemoveAt(path.Count - 1);
        }
    }

    private static void AdvanceInnermostArray(
        List<string> path,
        Stack<(int PathPosition, int Index)> arrays
    )
    {
        if (arrays.Count == 0)
            return;

        var current = arrays.Pop();
        var nextIndex = current.Index + 1;
        arrays.Push((current.PathPosition, nextIndex));
        path[current.PathPosition] = nextIndex.ToString(
            System.Globalization.CultureInfo.InvariantCulture
        );
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
            or TokenType.Bool => Encoding.UTF8.GetString(reader.ValueSpan),
            _ => "",
        };
    }
}
