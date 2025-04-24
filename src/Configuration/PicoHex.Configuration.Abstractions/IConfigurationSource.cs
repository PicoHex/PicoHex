namespace PicoHex.Configuration.Abstractions;

public interface IConfigurationSource
{
    // 明确数据源的优先级（覆盖顺序）
    int Priority { get; }

    // 支持异步加载
    Task<IConfigurationDataProvider> BuildAsync();
}
