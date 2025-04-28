namespace PicoHex.Configuration.Abstractions;

public interface ICfg
{
    string? this[string key] { get; set; }
    IEnumerable<ICfgSection> GetChildren();
    ICfgSection GetSection(string key);
}

public interface ICfgRoot : ICfg
{
    IEnumerable<ICfgProvider> Providers { get; }
    void Reload();
}

public interface ICfgSection : ICfg
{
    string Path { get; }
    string Key { get; }
    string? Value { get; set; }
}
