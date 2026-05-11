namespace PicoCfg;

internal static class CfgSourceHelpers
{
    public static async ValueTask<ICfgProvider> OpenAsync(ICfgProvider provider, CancellationToken ct)
    {
        await provider.ReloadAsync(ct);
        return provider;
    }
}
