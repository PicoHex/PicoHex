namespace PicoHex.Configuration.Abstractions;

public interface IConfigurationDataProvider : IDisposable
{
    // 提供数据版本标识（用于变更检测）
    string Version { get; }

    // 异步加载原始数据（如从文件、网络、数据库）
    Task LoadAsync();

    // 获取结构化数据（如 JSON 节点、XML 元素）
    IConfigurationNode GetData();

    // 监听数据变更（基于事件或 Reactive 流）
    IObservable<ConfigurationReloadEvent> Reloaded { get; }
}
