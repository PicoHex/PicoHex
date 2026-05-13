namespace PicoCfg.DI.Tests;

internal static class CfgBindTestHelper
{
    public static void RegisterOptionsTargetBinding()
    {
        CfgBindRuntime.Register<CfgOptionsExtensionsTests.OptionsTarget>(
            CfgBindRuntime.ContractVersion,
            bind: (cfg, section) => BindOptionsTarget(cfg, section),
            tryBind: TryBindOptionsTarget,
            bindInto: BindIntoOptionsTarget
        );
    }

    private static CfgOptionsExtensionsTests.OptionsTarget BindOptionsTarget(ICfg cfg, string? section) => new()
    {
        Name = cfg.GetValue(CfgBindRuntime.CombinePath(section, "Name")),
        Count = ParseInt(cfg, CfgBindRuntime.CombinePath(section, "Count")),
    };

    private static bool TryBindOptionsTarget(
        ICfg cfg,
        string? section,
        out CfgOptionsExtensionsTests.OptionsTarget value
    )
    {
        value = new CfgOptionsExtensionsTests.OptionsTarget
        {
            Name = cfg.GetValue(CfgBindRuntime.CombinePath(section, "Name")),
            Count = ParseInt(cfg, CfgBindRuntime.CombinePath(section, "Count")),
        };
        return true;
    }

    private static void BindIntoOptionsTarget(
        ICfg cfg,
        string? section,
        CfgOptionsExtensionsTests.OptionsTarget instance
    )
    {
        instance.Name = cfg.GetValue(CfgBindRuntime.CombinePath(section, "Name"));
        var raw = cfg.GetValue(CfgBindRuntime.CombinePath(section, "Count"));
        if (raw is not null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c))
            instance.Count = c;
    }

    private static int ParseInt(ICfg cfg, string path)
    {
        var raw = cfg.GetValue(path);
        return raw is not null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }
}
