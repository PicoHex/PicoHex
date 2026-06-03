using System.Text;
using PicoCfg.Abs;
using PicoJetson;
using PicoSerDe.Core;

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
        /// </summary>
        public CfgBuilder AddJson(string json)
        {
            ArgumentNullException.ThrowIfNull(json);
            var bytes = Encoding.UTF8.GetBytes(json);
            return builder.AddCustomSource(new JsonCfgSource(bytes));
        }

        /// <summary>
        /// Adds a JSON file as a configuration source.
        /// The JSON is flattened into key:value pairs using ':' as the hierarchy separator.
        /// </summary>
        /// <param name="path">The path to the JSON file.</param>
        public CfgBuilder AddJsonFile(string path, Encoding? encoding = null)
        {
            ArgumentNullException.ThrowIfNull(path);
            var bytes = File.ReadAllBytes(path);
            return builder.AddCustomSource(new JsonCfgSource(bytes));
        }
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
    public ICfgSnapshot Snapshot { get; } =
        new JsonCfgSnapshot(JsonFlattener.Flatten(jsonBytes));

    public ValueTask<bool> ReloadAsync(CancellationToken ct = default) =>
        ValueTask.FromResult(false);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class JsonCfgSnapshot(Dictionary<string, string> values) : ICfgSnapshot
{
    public bool TryGetValue(string path, out string? value) =>
        values.TryGetValue(path, out value);
}
