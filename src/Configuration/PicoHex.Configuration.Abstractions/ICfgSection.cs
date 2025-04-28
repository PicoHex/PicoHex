namespace PicoHex.Configuration.Abstractions;

public interface ICfgSection : ICfg
{
    string Path { get; }
    string Key { get; }
    string? Value { get; set; }
}
