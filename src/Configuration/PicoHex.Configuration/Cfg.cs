namespace PicoHex.Configuration;

public static class Cfg
{
    public static ICfgBuilder CreateBuilder() => new CfgBuilder();
}
