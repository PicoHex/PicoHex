namespace PicoHex.Configuration.Abstractions;

public interface ICfgProvider
{
    IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string parentPath);
    void Load();
    void Set(string key, string? value);
    bool TryGet(string key, out string? value);
}
