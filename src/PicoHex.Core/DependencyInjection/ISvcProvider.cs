namespace PicoHex.Core.DependencyInjection;

public interface ISvcProvider : IResolver
{
    ISvcScope CreateScope();
}
