namespace PicoCfg.Yaml;

public static class CfgBuilderYamlExtensions
{
    extension(CfgBuilder builder)
    {
        public CfgBuilder AddYaml(string yaml)
        {
            ArgumentNullException.ThrowIfNull(yaml);
            return builder.AddCustomSource(new YamlCfgSource(Encoding.UTF8.GetBytes(yaml)));
        }

        public CfgBuilder AddYamlFile(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            return builder.AddSource(
                builder.CreateFileWatchingSource(
                    path,
                    ParseYamlFileAsync,
                    () => File.GetLastWriteTimeUtc(path)
                )
            );
        }
    }

    private static Task<Dictionary<string, string>> ParseYamlFileAsync(
        Stream stream,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(YamlFlattener.Flatten(buffer.ToArray()));
    }
}

internal sealed class YamlCfgSource(byte[] yamlBytes) : ICfgSource
{
    public ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default) =>
        ValueTask.FromResult<ICfgProvider>(new YamlCfgProvider(yamlBytes));
}

internal sealed class YamlCfgProvider(byte[] yamlBytes) : ICfgProvider
{
    public ICfgSnapshot Snapshot { get; } = new YamlCfgSnapshot(YamlFlattener.Flatten(yamlBytes));

    public ValueTask<bool> ReloadAsync(CancellationToken ct = default) =>
        ValueTask.FromResult(false);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class YamlCfgSnapshot(Dictionary<string, string> values) : ICfgSnapshot
{
    public bool TryGetValue(string path, out string? value) => values.TryGetValue(path, out value);

    public IReadOnlyDictionary<string, string> GetAllValues() => values;
}
