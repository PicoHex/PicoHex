namespace PicoHex.Configuration.Abstractions;

/// <summary>
/// 表示配置源重载动作完成的事件
/// </summary>
public sealed class ConfigurationReloadEvent : EventArgs
{
    /// <summary>
    /// 重载是否成功
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// 错误信息（如果 Succeeded 为 false）
    /// </summary>
    public Exception Error { get; }

    /// <summary>
    /// 重载耗时（毫秒）
    /// </summary>
    public long DurationMilliseconds { get; }

    /// <summary>
    /// 数据源标识（如 "appsettings.json"）
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// 重载触发方式（手动/自动）
    /// </summary>
    public ReloadTriggerType TriggerType { get; }

    public ConfigurationReloadEvent(
        string sourceId,
        bool succeeded,
        Exception error,
        long durationMilliseconds,
        ReloadTriggerType triggerType
    )
    {
        SourceId = sourceId ?? throw new ArgumentNullException(nameof(sourceId));
        Succeeded = succeeded;
        Error = error;
        DurationMilliseconds = durationMilliseconds;
        TriggerType = triggerType;
    }
}

public enum ReloadTriggerType
{
    Manual, // 手动调用 Reload()
    Automatic // 自动检测到变更（如文件监视器）
}
