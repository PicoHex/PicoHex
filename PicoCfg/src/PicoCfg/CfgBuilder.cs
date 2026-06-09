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
    /// <summary>
    /// Adds a custom <see cref="ICfgSource"/> implementation to the builder.
    /// This is the extension point for third-party configuration sources
    /// (e.g. database, Redis, REST API).
    /// Sources are evaluated in insertion order; later sources override earlier ones.
    /// </summary>
    public CfgBuilder AddCustomSource(ICfgSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _sources.Add(source);
        return this;
    }

    internal CfgBuilder AddSource(ICfgSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _sources.Add(source);
        return this;
    }

    /// <summary>
    /// Optional callback for observing file watching errors (reload failures,
    /// watcher cleanup errors, dispose errors). Receives context string and exception.
    /// </summary>
    public Action<string, Exception>? OnFileWatchError { get; set; }

    /// <summary>
    /// Optional callback for observing format errors during configuration parsing.
    /// Receives the malformed line content and its line number (0-based).
    /// Lines without a '=' separator are silently skipped if this is null.
    /// </summary>
    public Action<string, int>? OnFormatError { get; set; }

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
        var onFormatError = OnFormatError;
        return new StreamCfgSource(() =>
            new StreamCfgProvider(
                streamFactory,
                versionStampFactory,
                (stream, ct) => ParseStreamAsync(stream, ct, encoding, onFormatError),
                CreateProviderState()
            )
        );
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
        return new FileWatchingCfgSource(innerSource, filePath, debounceInterval, OnFileWatchError);
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
        return new DictionaryCfgSource(() =>
            new DictionaryCfgProvider(dataFactory, versionStampFactory, CreateProviderState())
        );
    }

    internal CfgProviderState CreateProviderState() =>
        new(
            static () => new CfgChangeSignal(),
            static (values, fingerprint) => new CfgSnapshot(values, fingerprint)
        );

    internal Func<IReadOnlyList<ICfgSnapshot>, ICfgSnapshot> CreateSnapshotComposer() =>
        providerSnapshots =>
            CfgSnapshotComposer.CreateSnapshot(
                providerSnapshots,
                static (values, fingerprint) => new CfgSnapshot(values, fingerprint)
            );

    private static async ValueTask DisposeProvidersAsync(IReadOnlyList<ICfgProvider> providers)
    {
        for (var i = providers.Count - 1; i >= 0; i--)
        {
            try
            {
                await providers[i].DisposeAsync();
            }
            catch
            { /* best-effort */
            }
        }
    }

    private static async Task<Dictionary<string, string>> ParseStreamAsync(
        Stream stream,
        CancellationToken ct,
        Encoding? encoding = null,
        Action<string, int>? onFormatError = null
    )
    {
        using var reader = encoding is null
            ? new StreamReader(stream, leaveOpen: true)
            : new StreamReader(stream, encoding, leaveOpen: true);

        var newData = new Dictionary<string, string>();
        var lineNumber = 0;
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            lineNumber++;
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0)
                continue;

            // Skip full-line comments (# or //)
            if (trimmed[0] == '#' || trimmed.StartsWith("//", StringComparison.Ordinal))
                continue;

            // Find the '=' separator that is NOT inside quotes
            var separatorIndex = -1;
            var inQuote = false;
            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
                    inQuote = !inQuote;
                else if (line[i] == '=' && !inQuote)
                {
                    separatorIndex = i;
                    break;
                }
            }

            if (separatorIndex < 0)
            {
                onFormatError?.Invoke(line, lineNumber);
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var rawValue = line[(separatorIndex + 1)..];

            // Strip inline comments from value (# or // preceded by whitespace),
            // but only outside quoted regions
            var valueEnd = rawValue.Length;
            var inValueQuote = false;
            for (var i = 0; i < rawValue.Length; i++)
            {
                // Track quote state to avoid treating # inside quotes as comments
                if (rawValue[i] == '"' && (i == 0 || rawValue[i - 1] != '\\'))
                {
                    inValueQuote = !inValueQuote;
                    continue;
                }

                if (inValueQuote)
                    continue;

                if (rawValue[i] == '#' && (i == 0 || char.IsWhiteSpace(rawValue[i - 1])))
                {
                    valueEnd = i;
                    break;
                }
                if (
                    i + 1 < rawValue.Length
                    && rawValue[i] == '/'
                    && rawValue[i + 1] == '/'
                    && (i == 0 || char.IsWhiteSpace(rawValue[i - 1]))
                )
                {
                    valueEnd = i;
                    break;
                }
            }

            var value = rawValue[..valueEnd].Trim();

            // Strip surrounding quotes if present
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                value = value[1..^1];

            newData[key] = value;
        }
        return newData;
    }
}
