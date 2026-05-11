using System.Text;

[BenchmarkClass(Description = "Non-equivalent product-behavior reload controls")]
public partial class ReloadControlBenchmarks
{
    private IConfigurationRoot _msConfig = null!;
    private ICfgRoot _picoRoot = null!;
    private Dictionary<string, string> _dictionaryData = null!;

    [Params(100, 1000)]
    public int N { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _dictionaryData = new Dictionary<string, string>(N);
        for (var i = 0; i < N; i++)
            _dictionaryData[$"Section:Key{i}"] = $"Value{i}";

        _msConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(
                _dictionaryData.ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value)
            )
            .Build();

        var builder = Cfg.CreateBuilder();
        builder.Add(_dictionaryData);
        _picoRoot = builder.BuildAsync().AsTask().GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _picoRoot.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Benchmark]
    public void MsConfigMemoryReload()
    {
        _msConfig.Reload();
    }

    [Benchmark]
    public void PicoCfgDictionaryReload()
    {
        _ = _picoRoot.ReloadAsync().AsTask().GetAwaiter().GetResult();
    }
}
