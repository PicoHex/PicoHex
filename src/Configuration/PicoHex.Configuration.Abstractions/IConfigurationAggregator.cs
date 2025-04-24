namespace PicoHex.Configuration.Abstractions;

public interface IConfigurationAggregator
{
    // 动态添加/移除数据源
    void AddSource(IConfigurationSource source);
    void RemoveSource(string sourceId);

    // 合并所有数据源并生成最终配置
    Task<IConfigurationNode> BuildConfigurationAsync();

    // 全局配置变更事件
    IObservable<ConfigurationChangedEvent> ConfigurationChanged { get; }
}
