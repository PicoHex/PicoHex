namespace PicoCfg;

internal interface IInternalCfgRootSnapshotAccessor
{
    ICfgSnapshot CurrentSnapshot { get; }
}
