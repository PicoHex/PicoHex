namespace PicoCfg.Tests;

internal static class TestCfgFactory
{
    public static CfgProviderState CreateProviderState(
        Func<CfgChangeSignal>? changeSignalFactory = null,
        Func<IReadOnlyDictionary<string, string>, int, CfgSnapshot>? snapshotFactory = null
    )
    {
        return new CfgProviderState(
            changeSignalFactory ?? CfgBuilder.CreateDefaultChangeSignalFactory(),
            snapshotFactory ?? CfgBuilder.CreateDefaultSnapshotFactory()
        );
    }

    public static StreamCfgProvider CreateStreamProvider(
        Func<Stream> streamFactory,
        Func<object?>? versionStampFactory = null,
        Func<Stream, CancellationToken, Task<Dictionary<string, string>>>? streamParser = null,
        CfgProviderState? state = null
    )
    {
        return new StreamCfgProvider(
            streamFactory,
            versionStampFactory,
            streamParser ?? CfgBuilder.CreateDefaultStreamParser(),
            state ?? CreateProviderState()
        );
    }

    public static StreamCfgSource CreateStreamSource(
        Func<Stream> streamFactory,
        Func<object?>? versionStampFactory = null,
        Func<Stream, CancellationToken, Task<Dictionary<string, string>>>? streamParser = null,
        Func<CfgProviderState>? providerStateFactory = null
    )
    {
        return new StreamCfgSource(() =>
            CreateStreamProvider(
                streamFactory,
                versionStampFactory,
                streamParser,
                providerStateFactory?.Invoke() ?? CreateProviderState()
            )
        );
    }

    public static DictionaryCfgProvider CreateDictionaryProvider(
        Func<IEnumerable<KeyValuePair<string, string>>> dataFactory,
        Func<object?>? versionStampFactory = null,
        CfgProviderState? state = null
    )
    {
        return new DictionaryCfgProvider(dataFactory, versionStampFactory, state ?? CreateProviderState());
    }

    public static DictionaryCfgSource CreateDictionarySource(
        IDictionary<string, string> data,
        Func<object?>? versionStampFactory = null,
        Func<CfgProviderState>? providerStateFactory = null
    )
    {
        return CreateDictionarySource(() => data, versionStampFactory, providerStateFactory);
    }

    public static DictionaryCfgSource CreateDictionarySource(
        Func<IEnumerable<KeyValuePair<string, string>>> dataFactory,
        Func<object?>? versionStampFactory = null,
        Func<CfgProviderState>? providerStateFactory = null
    )
    {
        return new DictionaryCfgSource(() =>
            CreateDictionaryProvider(
                dataFactory,
                versionStampFactory,
                providerStateFactory?.Invoke() ?? CreateProviderState()
            )
        );
    }

    public static CfgRoot CreateRoot(
        IEnumerable<ICfgProvider> providers,
        Func<IReadOnlyList<ICfgSnapshot>, ICfgSnapshot>? snapshotComposer = null,
        Func<CfgChangeSignal>? changeSignalFactory = null
    )
    {
        return new CfgRoot(
            providers,
            snapshotComposer
                ?? CfgBuilder.CreateDefaultSnapshotComposer(CfgBuilder.CreateDefaultSnapshotFactory()),
            changeSignalFactory ?? CfgBuilder.CreateDefaultChangeSignalFactory()
        );
    }

    public static CfgRoot CreateRoot(params ICfgProvider[] providers)
    {
        return CreateRoot((IEnumerable<ICfgProvider>)providers);
    }
}
