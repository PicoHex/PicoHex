namespace PicoHex.Configuration.Abstractions;

public interface ICfgBuilder
{
    IDictionary<string, object> Properties { get; }
    IList<ICfgSource> Sources { get; }
    ICfgBuilder AddSource(ICfgSource source);
    ICfgRoot Build();
}
