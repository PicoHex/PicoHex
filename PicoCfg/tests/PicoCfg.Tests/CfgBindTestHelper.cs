namespace PicoCfg.Tests;

internal static class CfgBindTestHelper
{
    public static void RegisterTestClassBinding()
    {
        CfgBindRuntime.Register<CfgOptionsTests.TestClass>(
            CfgBindRuntime.ContractVersion,
            bind: (cfg, section) => BindTestClass(cfg, section),
            tryBind: TryBindTestClass,
            bindInto: BindIntoTestClass
        );
    }

    public static void RegisterValidatableTargetBinding()
    {
        CfgBindRuntime.Register<CfgValidatorTests.ValidatableTarget>(
            CfgBindRuntime.ContractVersion,
            bind: (cfg, section) => BindValidatableTarget(cfg, section),
            tryBind: TryBindValidatableTarget,
            bindInto: BindIntoValidatableTarget
        );
    }

    private static CfgOptionsTests.TestClass BindTestClass(ICfg cfg, string? section) => new()
    {
        Name = cfg.GetValue(CfgBindRuntime.CombinePath(section, "Name")),
        Count = ParseInt(cfg, CfgBindRuntime.CombinePath(section, "Count")),
    };

    private static bool TryBindTestClass(ICfg cfg, string? section, out CfgOptionsTests.TestClass value)
    {
        value = new CfgOptionsTests.TestClass
        {
            Name = cfg.GetValue(CfgBindRuntime.CombinePath(section, "Name")),
            Count = ParseInt(cfg, CfgBindRuntime.CombinePath(section, "Count")),
        };
        return true;
    }

    private static void BindIntoTestClass(ICfg cfg, string? section, CfgOptionsTests.TestClass instance)
    {
        instance.Name = cfg.GetValue(CfgBindRuntime.CombinePath(section, "Name"));
        var raw = cfg.GetValue(CfgBindRuntime.CombinePath(section, "Count"));
        if (raw is not null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var c))
            instance.Count = c;
    }

    private static CfgValidatorTests.ValidatableTarget BindValidatableTarget(ICfg cfg, string? section) => new()
    {
        Name = cfg.GetValue(CfgBindRuntime.CombinePath(section, "Name")),
        Count = ParseInt(cfg, CfgBindRuntime.CombinePath(section, "Count")),
    };

    private static bool TryBindValidatableTarget(
        ICfg cfg,
        string? section,
        out CfgValidatorTests.ValidatableTarget value
    )
    {
        value = new CfgValidatorTests.ValidatableTarget
        {
            Name = cfg.GetValue(CfgBindRuntime.CombinePath(section, "Name")),
            Count = ParseInt(cfg, CfgBindRuntime.CombinePath(section, "Count")),
        };
        return true;
    }

    private static void BindIntoValidatableTarget(
        ICfg cfg,
        string? section,
        CfgValidatorTests.ValidatableTarget instance
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
