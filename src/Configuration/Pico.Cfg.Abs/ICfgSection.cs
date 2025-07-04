namespace Pico.CFG.Abs;

public interface ICFGSection : ICFG
{
    string Path { get; }
    string Key { get; }
    string? Value { get; set; }
}
