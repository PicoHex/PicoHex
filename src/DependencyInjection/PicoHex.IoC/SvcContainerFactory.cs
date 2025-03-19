namespace PicoHex.IoC;

public class SvcContainerFactory(ISvcProviderFactory providerFactory)
{
    public ISvcContainer CreateContainer() => new SvcContainer(providerFactory);
}
