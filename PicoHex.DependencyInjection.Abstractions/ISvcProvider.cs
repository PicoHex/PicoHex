namespace PicoHex.DependencyInjection.Abstractions;

public interface ISvcProvider : IResolver
{
    ISvcScope CreateScope();
}
