namespace Pico.Cfg;

public static class Cfg
{
    public static ICfgBuilder CreateBuilder() => new CfgBuilder();
}
