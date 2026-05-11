[BenchmarkClass(Description = "Look up configuration values by key")]
public partial class LookupBenchmarks
{
    private IConfigurationRoot _msConfig = null!;
    private ICfgRoot _picoRoot = null!;
    private string[] _keys = null!;
    private IReadOnlyList<Dictionary<string, string>> _dataSets = null!;

    [Params(100, 1000)]
    public int N { get; set; }

    [Params(1, 4)]
    public int ProviderCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dataSets = new List<Dictionary<string, string>>(ProviderCount);
        for (var providerIndex = 0; providerIndex < ProviderCount; providerIndex++)
        {
            var data = new Dictionary<string, string>(N);
            for (var i = 0; i < N; i++)
                data[$"Section:Key{i}"] = $"Provider{providerIndex}:Value{i}";

            dataSets.Add(data);
        }

        _dataSets = dataSets;

        _keys = _dataSets[^1].Keys.ToArray();

        var msBuilder = new ConfigurationBuilder();
        for (var i = 0; i < _dataSets.Count; i++)
        {
            msBuilder.AddInMemoryCollection(
                _dataSets[i].ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value)
            );
        }

        _msConfig = msBuilder.Build();

        var builder = Cfg.CreateBuilder();
        for (var i = 0; i < _dataSets.Count; i++)
            builder.Add(_dataSets[i]);

        _picoRoot = builder.BuildAsync().AsTask().GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _picoRoot.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark(Baseline = true)]
    public void MsConfig()
    {
        for (var i = 0; i < _keys.Length; i++)
            _ = _msConfig[_keys[i]];
    }

    [Benchmark]
    public void PicoCfg()
    {
        for (var i = 0; i < _keys.Length; i++)
            _ = _picoRoot.GetValue(_keys[i]);
    }
}
