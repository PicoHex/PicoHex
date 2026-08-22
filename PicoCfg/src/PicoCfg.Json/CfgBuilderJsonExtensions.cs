namespace PicoCfg.Json;

/// <summary>
/// JSON configuration source extensions for <see cref="CfgBuilder"/>.
/// Uses PicoJetson for AOT-compatible zero-reflection JSON parsing.
/// </summary>
public static class CfgBuilderJsonExtensions
{
    extension(CfgBuilder builder)
    {
        /// <summary>
        /// Adds a JSON string as a configuration source.
        /// The JSON is flattened into key:value pairs using ':' as the hierarchy separator.
        /// Nested objects become compound keys; arrays are skipped unless
        /// <paramref name="flattenArrays"/> is set.
        /// The in-memory string source does NOT watch for changes.
        /// </summary>
        /// <param name="json">The JSON document to parse.</param>
        /// <param name="flattenArrays">
        /// When <see langword="true"/>, array elements are flattened with numeric
        /// index segments: <c>{"Items":[1,2]}</c> produces <c>Items:0</c> and
        /// <c>Items:1</c>; objects inside arrays extend the path (<c>Items:0:Name</c>).
        /// Defaults to <see langword="false"/> (arrays are skipped).
        /// </param>
        public CfgBuilder AddJson(string json, bool flattenArrays = false)
        {
            ArgumentNullException.ThrowIfNull(json);
            var bytes = Encoding.UTF8.GetBytes(json);
            return builder.AddCustomSource(new JsonCfgSource(bytes, flattenArrays));
        }

        /// <summary>
        /// Adds a JSON file as a configuration source with file-change
        /// auto-reload: the file is re-parsed (with debounce) after changes
        /// and the new values are published on the next root reload.
        /// The JSON is flattened into key:value pairs using ':' as the
        /// hierarchy separator; arrays are skipped unless
        /// <paramref name="flattenArrays"/> is set.
        /// </summary>
        /// <param name="path">The path to the JSON file.</param>
        /// <param name="flattenArrays">
        /// When <see langword="true"/>, array elements are flattened with numeric
        /// index segments (see <see cref="AddJson"/>).
        /// Defaults to <see langword="false"/> (arrays are skipped).
        /// </param>
        public CfgBuilder AddJsonFile(string path, bool flattenArrays = false)
        {
            ArgumentNullException.ThrowIfNull(path);
            return builder.AddSource(
                builder.CreateFileWatchingSource(
                    path,
                    (stream, ct) => ParseJsonFileAsync(stream, ct, flattenArrays),
                    () => File.GetLastWriteTimeUtc(path)
                )
            );
        }
    }

    private static Task<Dictionary<string, string>> ParseJsonFileAsync(
        Stream stream,
        CancellationToken ct,
        bool flattenArrays = false
    )
    {
        ct.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(JsonFlattener.Flatten(buffer.ToArray(), flattenArrays));
    }
}

internal sealed class JsonCfgSource(byte[] jsonBytes, bool flattenArrays = false) : ICfgSource
{
    public ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        var provider = new JsonCfgProvider(jsonBytes, flattenArrays);
        return ValueTask.FromResult<ICfgProvider>(provider);
    }
}

internal sealed class JsonCfgProvider(byte[] jsonBytes, bool flattenArrays = false) : ICfgProvider
{
    public ICfgSnapshot Snapshot { get; } =
        new JsonCfgSnapshot(JsonFlattener.Flatten(jsonBytes, flattenArrays));

    public ValueTask<bool> ReloadAsync(CancellationToken ct = default) =>
        ValueTask.FromResult(false);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class JsonCfgSnapshot(Dictionary<string, string> values) : ICfgSnapshot
{
    public bool TryGetValue(string path, out string? value) => values.TryGetValue(path, out value);

    public IReadOnlyDictionary<string, string> GetAllValues() => values;
}
