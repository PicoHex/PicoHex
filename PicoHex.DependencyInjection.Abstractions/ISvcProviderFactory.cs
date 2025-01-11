namespace PicoHex.DependencyInjection.Abstractions;

public interface ISvcProviderFactory
{
    ISvcProvider CreateServiceProvider(ISvcRegistry registry);
}
