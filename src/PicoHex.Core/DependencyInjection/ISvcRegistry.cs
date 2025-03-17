namespace PicoHex.Core.DependencyInjection;

public interface ISvcRegistry
{
    ISvcProvider CreateProvider();
}
