using System.Text;

[BenchmarkClass(Description = "Reload configuration from PicoCfg in-memory sources")]
public partial class ReloadBenchmarks
{
    private ICfgRoot _dictionaryRoot = null!;
    private ICfgRoot _dictionaryStampedRoot = null!;
    private ICfgRoot _multiProviderRoot = null!;
    private ICfgRoot _streamRoot = null!;
    private ICfgRoot _streamStampedRoot = null!;
    private Dictionary<string, string> _dictionaryData = null!;
    private int _dictionaryVersionStamp;
    private string _streamContent = null!;
    private int _streamVersionStamp;

    [Params(100, 1000)]
    public int N { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _dictionaryData = new Dictionary<string, string>(N);
        for (var i = 0; i < N; i++)
            _dictionaryData[$"Section:Key{i}"] = $"Value{i}";

        _streamContent = string.Join(Environment.NewLine, _dictionaryData.Select(pair => $"{pair.Key}={pair.Value}"));

        var builder = Cfg.CreateBuilder();
        builder.Add(_dictionaryData);
        _dictionaryRoot = builder.BuildAsync().AsTask().GetAwaiter().GetResult();

        var stampedBuilder = Cfg.CreateBuilder();
        stampedBuilder.Add(_dictionaryData, () => _dictionaryVersionStamp);
        _dictionaryStampedRoot = stampedBuilder.BuildAsync().AsTask().GetAwaiter().GetResult();

        var streamBuilder = Cfg.CreateBuilder();
        streamBuilder.Add(() => new MemoryStream(Encoding.UTF8.GetBytes(_streamContent)));
        _streamRoot = streamBuilder.BuildAsync().AsTask().GetAwaiter().GetResult();

        var stampedStreamBuilder = Cfg.CreateBuilder();
        stampedStreamBuilder.Add(
            streamFactory: () => new MemoryStream(Encoding.UTF8.GetBytes(_streamContent)),
            versionStampFactory: () => _streamVersionStamp
        );
        _streamStampedRoot = stampedStreamBuilder.BuildAsync().AsTask().GetAwaiter().GetResult();

        var multiProviderBuilder = Cfg.CreateBuilder();
        multiProviderBuilder.Add(new Dictionary<string, string>(_dictionaryData));
        multiProviderBuilder.Add(new Dictionary<string, string>(_dictionaryData));
        _multiProviderRoot = multiProviderBuilder.BuildAsync().AsTask().GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _dictionaryRoot.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _dictionaryStampedRoot.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _multiProviderRoot.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _streamRoot.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _streamStampedRoot.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public void PicoCfgDictionaryUnchanged()
    {
        _ = _dictionaryRoot.ReloadAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    public void PicoCfgDictionaryUnchangedWithStamp()
    {
        _ = _dictionaryStampedRoot.ReloadAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    public void PicoCfgDictionaryChanged()
    {
        _dictionaryData["Section:Key0"] = $"Changed-{Environment.TickCount64}";
        _dictionaryVersionStamp++;
        _ = _dictionaryStampedRoot.ReloadAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    public void PicoCfgStreamUnchanged()
    {
        _ = _streamRoot.ReloadAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    public void PicoCfgStreamUnchangedWithStamp()
    {
        _ = _streamStampedRoot.ReloadAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    public void PicoCfgStreamChanged()
    {
        _streamContent = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, N).Select(i => $"Section:Key{i}={(i == 0 ? $"Changed-{Environment.TickCount64}" : $"Value{i}")}")
        );
        _streamVersionStamp++;
        _ = _streamStampedRoot.ReloadAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    public void PicoCfgMultiProviderReload()
    {
        _ = _multiProviderRoot.ReloadAsync().AsTask().GetAwaiter().GetResult();
    }
}
