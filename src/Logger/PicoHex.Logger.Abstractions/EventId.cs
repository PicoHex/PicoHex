namespace PicoHex.Logger.Abstractions;

public readonly struct EventId
{
    public int Id { get; } // 数字标识（必选）
    public string? Name { get; } // 可读名称（可选但推荐）

    public EventId(int id, string? name = null)
    {
        Id = id;
        Name = name;
    }
}
