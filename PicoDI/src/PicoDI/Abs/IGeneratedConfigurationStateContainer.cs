namespace PicoDI.Abs;

/// <summary>
/// Opt-in capability for containers that can store generated-configuration state directly.
/// This preserves compatibility for plain <see cref="ISvcContainer"/> implementations via fallback tracking.
/// </summary>
internal interface IGeneratedConfigurationStateContainer
{
    public bool IsGeneratedConfigurationApplied { get; set; }
}
