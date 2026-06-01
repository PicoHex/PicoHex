PicoBench.Runner.Initialize();
var send = BenchmarkRunner.Run<SendBenchmarks>(BenchmarkConfig.Default);
var pub = BenchmarkRunner.Run<PublishBenchmarks>(BenchmarkConfig.Default);
Console.WriteLine(
    $"Send/Publish benchmarks complete: {send.Comparisons?.Count ?? 0}+{pub.Comparisons?.Count ?? 0} results."
);
