namespace PicoCfg;

/// <summary>
/// Collects configuration sources and builds a composed configuration root.
/// Later-added sources have higher lookup precedence in the built root.
/// </summary>
public sealed class CfgBuilder
{
    /// <summary>
    /// Default delegate that parses a <see cref="Stream"/> into a key-value dictionary
    /// using PicoCfg's built-in line-based <c>key=value</c> parser.
    /// </summary>
    private static readonly Func<
        Stream,
        CancellationToken,
        Task<Dictionary<string, string>>
    > SDefaultStreamParser = static (stream, ct) => ParseStreamAsync(stream, ct);

    /// <summary>
    /// Default delegate that creates a new <see cref="CfgChangeSignal"/> instance.
    /// Used by the built-in provider state factory.
    /// </summary>
    private static readonly Func<CfgChangeSignal> SDefaultChangeSignalFactory = static () =>
        new CfgChangeSignal();

    /// <summary>
    /// Default delegate that creates a <see cref="CfgSnapshot"/> from a dictionary of values
    /// and a fingerprint. Used by the built-in snapshot composer.
    /// </summary>
    private static readonly Func<
        IReadOnlyDictionary<string, string>,
        int,
        CfgSnapshot
    > SDefaultSnapshotFactory = static (values, fingerprint) =>
        new CfgSnapshot(values, fingerprint);

    private readonly List<ICfgSource> _sources = [];
    private Func<Stream, CancellationToken, Task<Dictionary<string, string>>> _streamParser =
        SDefaultStreamParser;

    private Func<CfgChangeSignal> _changeSignalFactory = SDefaultChangeSignalFactory;

    private Func<IReadOnlyDictionary<string, string>, int, CfgSnapshot> _snapshotFactory =
        SDefaultSnapshotFactory;

    private Func<
        IReadOnlyList<ICfgSnapshot>,
        Func<IReadOnlyDictionary<string, string>, int, CfgSnapshot>,
        ICfgSnapshot
    >? _snapshotComposerOverride;

    private Func<
        Func<CfgChangeSignal>,
        Func<IReadOnlyDictionary<string, string>, int, CfgSnapshot>,
        CfgProviderState
    >? _providerStateFactoryOverride;

    /// <summary>
    /// Returns the built-in line-based <c>key=value</c> parser used by PicoCfg's text and stream sources.
    /// Use this when you want to decorate the default parsing behavior instead of replacing it outright.
    /// </summary>
    internal static Func<
        Stream,
        CancellationToken,
        Task<Dictionary<string, string>>
    > CreateDefaultStreamParser()
    {
        return SDefaultStreamParser;
    }

    internal static Func<CfgChangeSignal> CreateDefaultChangeSignalFactory()
    {
        return SDefaultChangeSignalFactory;
    }

    internal static Func<
        IReadOnlyDictionary<string, string>,
        int,
        CfgSnapshot
    > CreateDefaultSnapshotFactory()
    {
        return SDefaultSnapshotFactory;
    }

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

    internal Func<
        Stream,
        CancellationToken,
        Task<Dictionary<string, string>>
    > CreateStreamParser() => _streamParser;

    internal ICfgRoot CreateRoot(IEnumerable<ICfgProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        return new CfgRoot(providers, CreateSnapshotComposer(), _changeSignalFactory);
    }

    internal ICfgSource CreateStreamSource(
        Func<Stream> streamFactory,
        Func<object?>? versionStampFactory = null,
        Encoding? encoding = null
    )
    {
        ArgumentNullException.ThrowIfNull(streamFactory);

        var parser = encoding is null
            ? CreateStreamParser()
            : (stream, ct) => ParseStreamAsync(stream, ct, encoding);

        return new StreamCfgSource(
            () =>
                new StreamCfgProvider(
                    streamFactory,
                    versionStampFactory,
                    parser,
                    CreateProviderState()
                )
        );
    }

    internal ICfgSource CreateStreamSource(
        Func<Stream> streamFactory,
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
            () => new DictionaryCfgProvider(dataFactory, versionStampFactory, CreateProviderState())
        );
    }

    internal CfgProviderState CreateProviderState()
    {
        var changeSignalFactory = _changeSignalFactory;
        var snapshotFactory = _snapshotFactory;
        var providerStateFactoryOverride = _providerStateFactoryOverride;

        return providerStateFactoryOverride is null
            ? new CfgProviderState(changeSignalFactory, snapshotFactory)
            : providerStateFactoryOverride(changeSignalFactory, snapshotFactory);
    }

    internal Func<IReadOnlyList<ICfgSnapshot>, ICfgSnapshot> CreateSnapshotComposer()
    {
        var snapshotFactory = _snapshotFactory;
        var snapshotComposerOverride = _snapshotComposerOverride;

        return snapshotComposerOverride is null
            ? CreateDefaultSnapshotComposer(snapshotFactory)
            : providerSnapshots => snapshotComposerOverride(providerSnapshots, snapshotFactory);
    }

    /// <summary>
    /// Replaces the parser used by PicoCfg's built-in text and stream source paths.
    /// The supplied parser is called on each materialization that requires parsing source content.
    /// </summary>
    internal CfgBuilder WithStreamParser(
        Func<Stream, CancellationToken, Task<Dictionary<string, string>>> streamParser
    )
    {
        ArgumentNullException.ThrowIfNull(streamParser);
        _streamParser = streamParser;
        return this;
    }

    internal CfgBuilder WithChangeSignalFactory(Func<CfgChangeSignal> changeSignalFactory)
    {
        ArgumentNullException.ThrowIfNull(changeSignalFactory);
        _changeSignalFactory = changeSignalFactory;
        return this;
    }

    internal CfgBuilder WithSnapshotFactory(
        Func<IReadOnlyDictionary<string, string>, int, CfgSnapshot> snapshotFactory
    )
    {
        ArgumentNullException.ThrowIfNull(snapshotFactory);
        _snapshotFactory = snapshotFactory;
        return this;
    }

    /// <summary>
    /// Replaces the snapshot composer used to publish the root snapshot from the current provider snapshots.
    /// Use <see cref="CreateDefaultSnapshotComposer()"/> when you want to wrap the built-in composition behavior.
    /// </summary>
    internal CfgBuilder WithSnapshotComposer(
        Func<IReadOnlyList<ICfgSnapshot>, ICfgSnapshot> snapshotComposer
    )
    {
        ArgumentNullException.ThrowIfNull(snapshotComposer);
        _snapshotComposerOverride = (providerSnapshots, _) => snapshotComposer(providerSnapshots);
        return this;
    }

    internal CfgBuilder WithSnapshotComposer(
        Func<
            IReadOnlyList<ICfgSnapshot>,
            Func<IReadOnlyDictionary<string, string>, int, CfgSnapshot>,
            ICfgSnapshot
        > snapshotComposer
    )
    {
        ArgumentNullException.ThrowIfNull(snapshotComposer);
        _snapshotComposerOverride = snapshotComposer;
        return this;
    }

    internal CfgBuilder WithProviderStateFactory(Func<CfgProviderState> providerStateFactory)
    {
        ArgumentNullException.ThrowIfNull(providerStateFactory);
        _providerStateFactoryOverride = (_, _) => providerStateFactory();
        return this;
    }

    internal CfgBuilder WithProviderStateFactory(
        Func<
            Func<CfgChangeSignal>,
            Func<IReadOnlyDictionary<string, string>, int, CfgSnapshot>,
            CfgProviderState
        > providerStateFactory
    )
    {
        ArgumentNullException.ThrowIfNull(providerStateFactory);
        _providerStateFactoryOverride = providerStateFactory;
        return this;
    }

    /// <summary>
    /// Returns the built-in snapshot composer used by PicoCfg roots when no custom composer is supplied.
    /// Use this when you want to decorate the default provider-snapshot composition behavior.
    /// </summary>
    internal static Func<IReadOnlyList<ICfgSnapshot>, ICfgSnapshot> CreateDefaultSnapshotComposer()
    {
        return CreateDefaultSnapshotComposer(SDefaultSnapshotFactory);
    }

    internal static Func<IReadOnlyList<ICfgSnapshot>, ICfgSnapshot> CreateDefaultSnapshotComposer(
        Func<IReadOnlyDictionary<string, string>, int, CfgSnapshot> snapshotFactory
    )
    {
        ArgumentNullException.ThrowIfNull(snapshotFactory);
        return providerSnapshots =>
            CfgSnapshotComposer.CreateSnapshot(providerSnapshots, snapshotFactory);
    }

    private static async ValueTask DisposeProvidersAsync(IReadOnlyList<ICfgProvider> providers)
    {
        for (var i = providers.Count - 1; i >= 0; i--)
        {
            try
            {
                await providers[i].DisposeAsync();
            }
            catch
            {
                // Preserve the original build failure while still attempting full cleanup.
            }
        }
    }

    private static async Task<Dictionary<string, string>> ParseStreamAsync(
        Stream stream,
        CancellationToken ct,
        Encoding? encoding = null
    )
    {
        // leaveOpen: true — stream ownership stays with the provider,
        // not the parser. The provider disposes the stream after parsing.
        using var reader = encoding is null
            ? new StreamReader(stream, leaveOpen: true)
            : new StreamReader(stream, encoding, leaveOpen: true);

        var newData = new Dictionary<string, string>();
        while (await reader.ReadLineAsync(ct) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            newData[key] = value;
        }

        return newData;
    }
}
