namespace PicoHex.Core.DependencyInjection;

public interface ISvcProviderFactory
{
    ISvcProvider CreateServiceProvider(ISvcRegistry registry);
}
