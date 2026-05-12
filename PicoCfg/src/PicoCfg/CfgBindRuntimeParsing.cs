namespace PicoCfg;

partial class CfgBindRuntime
{
    public static bool TryParseBoolean(string? raw, out bool value) =>
        bool.TryParse(raw?.Trim(), out value);

    public static bool TryParseByte(string? raw, out byte value) =>
        byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseSByte(string? raw, out sbyte value) =>
        sbyte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseInt16(string? raw, out short value) =>
        short.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseUInt16(string? raw, out ushort value) =>
        ushort.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseInt32(string? raw, out int value) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseUInt32(string? raw, out uint value) =>
        uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseInt64(string? raw, out long value) =>
        long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseUInt64(string? raw, out ulong value) =>
        ulong.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    public static bool TryParseSingle(string? raw, out float value) =>
        float.TryParse(
            raw,
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out value
        );

    public static bool TryParseDouble(string? raw, out double value) =>
        double.TryParse(
            raw,
            NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture,
            out value
        );

    public static bool TryParseDecimal(string? raw, out decimal value) =>
        decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);

    public static bool TryParseGuid(string? raw, out Guid value) => Guid.TryParse(raw, out value);

    public static bool TryParseEnum<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] TEnum>(string? raw, out TEnum value)
        where TEnum : struct, Enum
    {
        try
        {
            value = (TEnum)Enum.Parse(typeof(TEnum), raw?.Trim()!, ignoreCase: true);
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }

    public static bool TryParseDateTime(string? raw, out DateTime value) =>
        DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    public static bool TryParseDateTimeOffset(string? raw, out DateTimeOffset value) =>
        DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    public static bool TryParseDateOnly(string? raw, out DateOnly value) =>
        DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    public static bool TryParseTimeOnly(string? raw, out TimeOnly value) =>
        TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);

    public static bool TryParseTimeSpan(string? raw, out TimeSpan value) =>
        TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out value);

    public static bool TryParseUri(string? raw, out Uri? value)
    {
        var trimmed = raw?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            value = null;
            return false;
        }

        if (Uri.TryCreate(trimmed, UriKind.RelativeOrAbsolute, out var uri))
        {
            value = uri;
            return true;
        }
        value = null;
        return false;
    }

    public static bool TryParseVersion(string? raw, out Version? value) =>
        Version.TryParse(raw?.Trim(), out value);

    public static bool TryParseBigInteger(string? raw, out System.Numerics.BigInteger value) =>
        System.Numerics.BigInteger.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// Creates a scoped configuration view that prepends <paramref name="section"/>
    /// to all key lookups. The returned view is a live delegation to the parent
    /// <paramref name="cfg"/> and reflects any reloads the parent observes.
    /// </summary>
    public static ICfg CreateScopedView(ICfg cfg, string? section)
        => new CfgSection(cfg, section ?? string.Empty);
}
