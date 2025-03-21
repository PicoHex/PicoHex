namespace PicoHex.Log.Extensions;

public static class ContainerExtensions
{
    public static ISvcContainer AddConsoleLogger(this ISvcContainer container)
    {
        return container;
    }
}
