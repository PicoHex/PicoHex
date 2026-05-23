namespace PicoCfg;

/// <summary>
/// Collects configuration sources and builds a composed configuration root.
/// Later-added sources have higher lookup precedence in the built root.
/// </summary>
public sealed class CfgBuilder
{
    private readonly List<ICfgSource> _sources = [];

    /// <summary>
    /// Adds a source to the builder.
    /// Sources are evaluated in insertion order, and later sources override earlier ones.
    /// </summary>
    internal CfgBuilder AddSource(ICfgSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _sources.Add(source);
        return this;
    }

    /// <summary>
    /// Opens all registered sources and returns a composed configuration root.
    /// The returned root owns the opened providers and should be disposed when no longer needed.
    /// </summary>
    public async ValueTask<ICfgRoot> BuildAsync(CancellationToken ct = default)
    {
        var providers = new List<ICfgProvider>();
        try
        {
            foreach (var source in _sources)
            {
                var provider = await source.OpenAsync(ct);
                providers.Add(provider);
            }
            return CreateRoot(providers);
        }
        catch
        {
            await DisposeProvidersAsync(providers);
            throw;
        }
    }

    internal ICfgRoot CreateRoot(IEnumerable<ICfgProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        return new CfgRoot(providers, CreateSnapshotComposer(), static () => new CfgChangeSignal());
    }

    internal ICfgSource CreateStreamSource(
        Func<CancellationToken, ValueTask<Stream>> streamFactory,
        Func<object?>? versionStampFactory = null,
        Encoding? encoding = null
    )
    {
        ArgumentNullException.ThrowIfNull(streamFactory);
        return new StreamCfgSource(
            () => new StreamCfgProvider(
                streamFactory, versionStampFactory,
                (stream, ct) => ParseStreamAsync(stream, ct, encoding),
                CreateProviderState()));
    }

    internal ICfgSource CreateStreamSource(
        Func<CancellationToken, ValueTask<Stream>> streamFactory,
        string filePath,
        Func<object?>? versionStampFactory = null,
        Encoding? encoding = null,
        TimeSpan? debounceInterval = null
    )
    {
        ArgumentNullException.ThrowIfNull(filePath);
        var innerSource = CreateStreamSource(streamFactory, versionStampFactory, encoding);
        return new FileWatchingCfgSource(innerSource, filePath, debounceInterval);
    }

    internal ICfgSource CreateDictionarySource(
        IDictionary<string, string> configData,
        Func<object?>? versionStampFactory = null
    )
    {
        ArgumentNullException.ThrowIfNull(configData);
        return CreateDictionarySource(() => configData, versionStampFactory);
    }

    internal ICfgSource CreateDictionarySource(
        Func<IEnumerable<KeyValuePair<string, string>>> dataFactory,
        Func<object?>? versionStampFactory = null
    )
    {
        ArgumentNullException.ThrowIfNull(dataFactory);
        return new DictionaryCfgSource(
            () => new DictionaryCfgProvider(dataFactory, versionStampFactory, CreateProviderState()));
    }

    internal CfgProviderState CreateProviderState() =>
        new(static () => new CfgChangeSignal(),
            static (values, fingerprint) => new CfgSnapshot(values, fingerprint));

    internal Func<IReadOnlyList<ICfgSnapshot>, ICfgSnapshot> CreateSnapshotComposer() =>
        providerSnapshots =>
            CfgSnapshotComposer.CreateSnapshot(providerSnapshots,
                static (values, fingerprint) => new CfgSnapshot(values, fingerprint));

    private static async ValueTask DisposeProvidersAsync(IReadOnlyList<ICfgProvider> providers)
    {
        for (var i = providers.Count - 1; i >= 0; i--)
        {
            try { await providers[i].DisposeAsync(); }
            catch { /* best-effort */ }
        }
    }

    private static async Task<Dictionary<string, string>> ParseStreamAsync(
        Stream stream, CancellationToken ct, Encoding? encoding = null)
    {
        using var reader = encoding is null
            ? new StreamReader(stream, leaveOpen: true)
            : new StreamReader(stream, encoding, leaveOpen: true);

        var newData = new Dictionary<string, string>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0) continue;
            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            newData[key] = value;
        }
        return newData;
    }
}
