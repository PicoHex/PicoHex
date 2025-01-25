namespace PicoHex.Logger.Abstractions;

public readonly struct EventId : IEquatable<EventId>
{
    private enum IdType
    {
        Int,
        Long,
        Guid
    }

    private readonly IdType _type;
    private readonly int _intId;
    private readonly long _longId;
    private readonly Guid _guidId;
    private readonly string? _name;

    public EventId(int id, string? name = null)
    {
        _type = IdType.Int;
        _intId = id;
        _longId = 0;
        _guidId = Guid.Empty;
        _name = name;
    }

    public EventId(long id, string? name = null)
    {
        _type = IdType.Long;
        _longId = id;
        _intId = 0;
        _guidId = Guid.Empty;
        _name = name;
    }

    public EventId(Guid id, string? name = null)
    {
        _type = IdType.Guid;
        _guidId = id;
        _intId = 0;
        _longId = 0;
        _name = name;
    }

    public static implicit operator EventId(int id) => new(id);

    public static implicit operator EventId(long id) => new(id);

    public static implicit operator EventId(Guid id) => new(id);

    public bool IsInt => _type == IdType.Int;
    public bool IsLong => _type == IdType.Long;
    public bool IsGuid => _type == IdType.Guid;

    public int IntId =>
        _type == IdType.Int
            ? _intId
            : throw new InvalidOperationException("Current Id is not Int type.");

    public long LongId =>
        _type == IdType.Long
            ? _longId
            : throw new InvalidOperationException("Current Id is not Long type.");

    public Guid GuidId =>
        _type == IdType.Guid
            ? _guidId
            : throw new InvalidOperationException("Current Id is not Guid type.");

    public string? Name => _name;

    // --------------------- 相等性比较 ---------------------
    public bool Equals(EventId other)
    {
        if (_type != other._type)
            return false;

        return _type switch
        {
            IdType.Int => _intId == other._intId,
            IdType.Long => _longId == other._longId,
            IdType.Guid => _guidId.Equals(other._guidId),
            _ => false
        };
    }

    public override bool Equals([NotNullWhen(true)] object? obj) =>
        obj is EventId other && Equals(other);

    public override int GetHashCode() =>
        _type switch
        {
            IdType.Int => _intId.GetHashCode(),
            IdType.Long => _longId.GetHashCode(),
            IdType.Guid => _guidId.GetHashCode(),
            _ => 0
        };

    public static bool operator ==(EventId left, EventId right) => left.Equals(right);

    public static bool operator !=(EventId left, EventId right) => !left.Equals(right);

    public override string ToString() =>
        _name
        ?? _type switch
        {
            IdType.Int => _intId.ToString(),
            IdType.Long => _longId.ToString(),
            IdType.Guid => _guidId.ToString(),
            _ => "Invalid"
        };
}
