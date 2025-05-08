namespace Pico.Cfg.Abs;

public interface ICfg
{
    string? this[string key] { get; set; }
    IEnumerable<ICfgSection> GetChildren();
    ICfgSection GetSection(string key);
}
