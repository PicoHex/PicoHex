namespace PicoHex.Abstractions.DependencyInjection;

public interface ISvcProviderFactory
{
    ISvcProvider CreateServiceProvider(ISvcRegistry registry);
}
