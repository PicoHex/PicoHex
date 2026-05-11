[BenchmarkClass(Description = "Build configuration root from in-memory key-value pairs")]
public partial class BuildBenchmarks
{
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
    }

    [Benchmark(Baseline = true)]
    public void MsConfig()
    {
        var builder = new ConfigurationBuilder();
        for (var i = 0; i < _dataSets.Count; i++)
        {
            builder.AddInMemoryCollection(
                _dataSets[i].ToDictionary(static pair => pair.Key, static pair => (string?)pair.Value)
            );
        }

        var config = builder.Build();

        _ = config["Section:Key0"];
    }

    [Benchmark]
    public void PicoCfg()
    {
        var builder = Cfg.CreateBuilder();
        for (var i = 0; i < _dataSets.Count; i++)
            builder.Add(_dataSets[i]);

        var root = builder.BuildAsync().AsTask().GetAwaiter().GetResult();

        _ = root.GetValue("Section:Key0");

        root.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
