namespace PicoCfg;

/// <summary>
/// Thrown when a generated PicoCfg.Gen binder registration is missing or incompatible
/// at <see cref="CfgBind"/> call sites.
/// </summary>
public sealed class PicoCfgBindRegistrationException : InvalidOperationException
{
    internal PicoCfgBindRegistrationException(string message)
        : base(message) { }

    /// <summary>
    /// Creates an exception indicating no generated registration was found for <paramref name="targetType"/>.
    /// </summary>
    public static PicoCfgBindRegistrationException CreateMissing(
        Type targetType,
        string operationName
    )
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentException.ThrowIfNullOrEmpty(operationName);

        return new PicoCfgBindRegistrationException(
            $"No generated PicoCfg.Gen registration was found for '{targetType.FullName}' while calling CfgBind.{operationName}<T>. "
                + "Ensure the consuming project references PicoCfg.Gen and uses either a direct closed generic CfgBind call or a direct closed generic PicoCfg.DI RegisterCfgTransient/Scoped/Singleton call so the source generator can register the binder."
        );
    }

    /// <summary>
    /// Creates an exception indicating the generated registration contract version
    /// (<paramref name="actualContractVersion"/>) does not match the expected
    /// <paramref name="expectedContractVersion"/>.
    /// </summary>
    public static PicoCfgBindRegistrationException CreateIncompatible(
        Type targetType,
        string operationName,
        int expectedContractVersion,
        int actualContractVersion
    )
    {
        ArgumentNullException.ThrowIfNull(targetType);
        ArgumentException.ThrowIfNullOrEmpty(operationName);

        return new PicoCfgBindRegistrationException(
            $"Generated PicoCfg.Gen registration for '{targetType.FullName}' is incompatible while calling CfgBind.{operationName}<T>. "
                + $"Expected contract version {expectedContractVersion}, but the generated registration reported version {actualContractVersion}."
        );
    }
}
