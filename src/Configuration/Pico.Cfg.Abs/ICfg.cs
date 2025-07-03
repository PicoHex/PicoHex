namespace Pico.CFG.Abs;

public interface ICFG
{
    string? this[string key] { get; set; }
    IEnumerable<ICFGSection> GetChildren();
    ICFGSection GetSection(string key);
}
