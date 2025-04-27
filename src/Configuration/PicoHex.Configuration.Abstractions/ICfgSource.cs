namespace PicoHex.Configuration.Abstractions;

public interface ICfgSource
{
    ICfgProvider Build(ICfgBuilder builder);
}
