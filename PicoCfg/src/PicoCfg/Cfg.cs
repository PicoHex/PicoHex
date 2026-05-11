namespace PicoCfg;

public static class Cfg
{
    /// <summary>
    /// Creates a builder for composing configuration sources into a single configuration root.
    /// </summary>
    public static CfgBuilder CreateBuilder() => new();
}
