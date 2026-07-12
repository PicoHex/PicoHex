namespace PicoCfg;

/// <summary>
/// Runtime infrastructure for source-generated PicoCfg.Gen binders.
/// Register, path composition, conversion errors, and registration storage
/// used by compile-time generated binding delegates.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static partial class CfgBindRuntime
{
    /// <summary>Version number used by the source generator to ensure generated code matches this runtime.</summary>
    public const int ContractVersion = 2;

    /// <summary>Registers source-generated binding delegates for <typeparamref name="T"/>.</summary>
    public static void Register<T>(
        int contractVersion,
        Func<ICfg, string?, T>? bind,
        PicoCfgGeneratedTryBindDelegate<T>? tryBind,
        PicoCfgGeneratedBindIntoDelegate<T> bindInto
    )
    {
        ArgumentNullException.ThrowIfNull(bindInto);
        PicoCfgBindRegistrationStore<T>.Registration = new PicoCfgBindRegistration<T>(
            contractVersion,
            bind,
            tryBind,
            bindInto
        );
    }

    /// <summary>Combines an optional section prefix with a property name into a configuration path.</summary>
    public static string CombinePath(string? section, string propertyName)
    {
        ArgumentException.ThrowIfNullOrEmpty(propertyName);
        return string.IsNullOrEmpty(section)
            ? propertyName
            : string.Concat(section, ":", propertyName);
    }

    /// <summary>
    /// Tries to get a configuration value by section + property name,
    /// with case-insensitive fallback. Fast path: exact match via <see cref="ICfg.TryGetValue"/>.
    /// Slow path: case-insensitive scan of the section-scoped key set.
    /// </summary>
    public static bool TryGetValueIgnoreCase(
        ICfg cfg,
        string? section,
        string propertyName,
        out string? value
    )
    {
        // Fast path: exact match (e.g., PascalCase config key = PascalCase property)
        var path = CombinePath(section, propertyName);
        if (cfg.TryGetValue(path, out value))
            return true;

        // Slow path: case-insensitive fallback (e.g., camelCase JSON/YAML keys)
        var scope = string.IsNullOrEmpty(section) ? cfg : cfg.GetSection(section);
        foreach (var (key, val) in scope.GetAll())
        {
            if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = val;
                return true;
            }
        }

        value = null;
        return false;
    }

    /// <summary>Creates a <see cref="FormatException"/> describing a configuration value conversion failure.</summary>
    public static FormatException CreateConversionException(
        string path,
        string targetTypeDisplayName,
        string memberDisplayName
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentException.ThrowIfNullOrEmpty(targetTypeDisplayName);
        ArgumentException.ThrowIfNullOrEmpty(memberDisplayName);

        return new FormatException(
            $"Configuration value at '{path}' could not be converted to '{targetTypeDisplayName}' for '{memberDisplayName}'."
        );
    }

    internal static PicoCfgBindRegistration<T> GetRequiredRegistration<T>(string operationName)
    {
        var registration = PicoCfgBindRegistrationStore<T>.Registration;
        if (registration is null)
            throw PicoCfgBindRegistrationException.CreateMissing(typeof(T), operationName);

        if (registration.ContractVersion != ContractVersion)
        {
            throw PicoCfgBindRegistrationException.CreateIncompatible(
                typeof(T),
                operationName,
                ContractVersion,
                registration.ContractVersion
            );
        }

        return registration;
    }

    private static class PicoCfgBindRegistrationStore<T>
    {
        public static volatile PicoCfgBindRegistration<T>? Registration;
    }
}
