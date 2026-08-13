namespace PicoCfg.Toml;

public static class CfgBuilderTomlExtensions
{
    extension(CfgBuilder builder)
    {
        public CfgBuilder AddToml(string toml)
        {
            ArgumentNullException.ThrowIfNull(toml);
            return builder.AddCustomSource(new TomlCfgSource(Encoding.UTF8.GetBytes(toml)));
        }

        public CfgBuilder AddTomlFile(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            return builder.AddSource(
                builder.CreateFileWatchingSource(
                    path,
                    ParseTomlFileAsync,
                    () => File.GetLastWriteTimeUtc(path)
                )
            );
        }
    }

    private static Task<Dictionary<string, string>> ParseTomlFileAsync(
        Stream stream,
        CancellationToken ct
    )
    {
        ct.ThrowIfCancellationRequested();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(TomlFlattener.Flatten(buffer.ToArray()));
    }
}

internal sealed class TomlCfgSource(byte[] tomlBytes) : ICfgSource
{
    public ValueTask<ICfgProvider> OpenAsync(CancellationToken ct = default) =>
        ValueTask.FromResult<ICfgProvider>(new TomlCfgProvider(tomlBytes));
}

internal sealed class TomlCfgProvider(byte[] tomlBytes) : ICfgProvider
{
    public ICfgSnapshot Snapshot { get; } = new TomlCfgSnapshot(TomlFlattener.Flatten(tomlBytes));

    public ValueTask<bool> ReloadAsync(CancellationToken ct = default) =>
        ValueTask.FromResult(false);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class TomlCfgSnapshot(Dictionary<string, string> values) : ICfgSnapshot
{
    public bool TryGetValue(string path, out string? value) => values.TryGetValue(path, out value);

    public IReadOnlyDictionary<string, string> GetAllValues() => values;
}
