// See https://aka.ms/new-console-template for more information

var builder = Cfg.CreateBuilder();

builder
    .AddInMemoryString(
        """
        Database.ConnectionString=localhost:3306
        FeatureFlags.EnableNewUI=true
        """
    )
    .AddInMemoryDictionary(
        new Dictionary<string, string> { ["Logging:Level"] = "Debug", ["Cache:Timeout"] = "300" }
    )
    .AddInMemoryStream(stream =>
    {
        using var writer = new StreamWriter(stream, leaveOpen: true);
        writer.WriteLine("AppName=MyTestApp");
        writer.WriteLine("Version=1.0.0");
    });

var configRoot = await builder.BuildAsync();

var timeout = await configRoot.GetValueAsync("Cache:Timeout"); // 返回 "300"

Console.ReadLine();
