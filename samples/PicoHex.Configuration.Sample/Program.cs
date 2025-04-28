// See https://aka.ms/new-console-template for more information

// 示例1：直接使用字符串
var builder = Cfg.CreateBuilder()
    .AddInMemoryString(
        """
                       Database.ConnectionString=localhost:3306
                       FeatureFlags.EnableNewUI=true
                       """
    );

// 示例2：使用字典
var configDict = new Dictionary<string, string>
{
    ["Logging:Level"] = "Debug",
    ["Cache:Timeout"] = "300"
};
builder.AddInMemoryDictionary(configDict);

// 示例3：自定义流写入
builder.AddInMemoryStream(stream =>
{
    using var writer = new StreamWriter(stream, leaveOpen: true);
    writer.WriteLine("AppName=MyTestApp");
    writer.WriteLine("Version=1.0.0");
});

// 构建配置
var configRoot = await builder.BuildAsync();

// 获取配置值
var timeout = await configRoot.GetValueAsync("Cache:Timeout"); // 返回 "300"

Console.ReadLine();
