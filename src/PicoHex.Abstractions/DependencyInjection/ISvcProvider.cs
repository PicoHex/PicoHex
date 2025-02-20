namespace PicoHex.Abstractions.DependencyInjection;

public interface ISvcProvider : IResolver
{
    ISvcScope CreateScope();
}
