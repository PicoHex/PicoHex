namespace Pico.Cfg.Abs;

public interface ICfgSection : ICfg
{
    string Path { get; }
    string Key { get; }
    string? Value { get; set; }
}
