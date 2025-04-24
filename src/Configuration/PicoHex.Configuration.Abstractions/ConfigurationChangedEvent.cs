namespace PicoHex.Configuration.Abstractions;

/// <summary>
/// 表示配置发生变更的事件数据
/// </summary>
public sealed class ConfigurationChangedEvent(
    string sourceId,
    ConfigurationChangeType changeType,
    IReadOnlyCollection<string> affectedPaths,
    IConfigurationNode oldState,
    IConfigurationNode newState
) : EventArgs
{
    /// <summary>
    /// 变更来源的唯一标识（如 "AzureKeyVault", "AppSettings.json"）
    /// </summary>
    public string SourceId { get; } = sourceId ?? throw new ArgumentNullException(nameof(sourceId));

    /// <summary>
    /// 变更发生的时间戳（UTC）
    /// </summary>
    public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// 变更类型（全量重载/部分更新）
    /// </summary>
    public ConfigurationChangeType ChangeType { get; } = changeType;

    /// <summary>
    /// 受影响的配置路径集合（对于 ChangeType.Partial）
    /// 示例: ["Database:ConnectionStrings", "FeatureFlags:NewCheckout"]
    /// </summary>
    public IReadOnlyCollection<string> AffectedPaths { get; } = affectedPaths ?? [];

    /// <summary>
    /// 变更前的配置快照（可选，根据配置存储策略决定）
    /// </summary>
    public IConfigurationNode OldState { get; } = oldState;

    /// <summary>
    /// 变更后的配置快照（完整或部分）
    /// </summary>
    public IConfigurationNode NewState { get; } =
        newState ?? throw new ArgumentNullException(nameof(newState));
}

public enum ConfigurationChangeType
{
    /// <summary>
    /// 全量重载（如文件重新加载）
    /// </summary>
    FullReload,

    /// <summary>
    /// 部分更新（如单个Key的修改）
    /// </summary>
    PartialUpdate
}
