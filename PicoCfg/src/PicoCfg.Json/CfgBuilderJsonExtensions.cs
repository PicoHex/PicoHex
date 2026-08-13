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
        /// Nested objects become compound keys; arrays are skipped.
        /// The in-memory string source does NOT watch for changes.
        /// </summary>
        public CfgBuilder AddJson(string json)
        {
            ArgumentNullException.ThrowIfNull(json);
            var bytes = Encoding.UTF8.GetBytes(json);
            return builder.AddCustomSource(new JsonCfgSource(bytes));
        }

        /// <summary>
        /// Adds a JSON file as a configuration source with file-change
        /// auto-reload: the file is re-parsed (with debounce) after changes
        /// and the new values are published on the next root reload.
        /// The JSON is flattened into key:value pairs using ':' as the
        /// hierarchy separator; arrays are skipped.
        /// </summary>
        /// <param name="path">The path to the JSON file.</param>
        public CfgBuilder AddJsonFile(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            return builder.AddSource(
                builder.CreateFileWatchingSource(
                    path,
                    ParseJsonFileAsync,
                    () => File.GetLastWriteTimeUtc(path)
                )
            );
        }
    }

    private static Task<Dictionary<string, string>> ParseJsonFileAsync(
        Stream stream,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(JsonFlattener.Flatten(buffer.ToArray()));
    }
}

internal sealed class JsonCfgSource(byte[] jsonBytes) : ICfgSource
{
    public ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default)
    {
        var provider = new JsonCfgProvider(jsonBytes);
        return ValueTask.FromResult<ICfgProvider>(provider);
    }
}

internal sealed class JsonCfgProvider(byte[] jsonBytes) : ICfgProvider
{
    public ICfgSnapshot Snapshot { get; } = new JsonCfgSnapshot(JsonFlattener.Flatten(jsonBytes));

    public ValueTask<bool> ReloadAsync(CancellationToken ct = default) =>
        ValueTask.FromResult(false);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class JsonCfgSnapshot(Dictionary<string, string> values) : ICfgSnapshot
{
    public bool TryGetValue(string path, out string? value) => values.TryGetValue(path, out value);

    public IReadOnlyDictionary<string, string> GetAllValues() => values;
}
