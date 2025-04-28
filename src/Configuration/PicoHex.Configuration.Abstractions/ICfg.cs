namespace PicoHex.Configuration.Abstractions;

public interface ICfg
{
    string? this[string key] { get; set; }
    IEnumerable<ICfgSection> GetChildren();
    ICfgSection GetSection(string key);
}
