namespace PicoCfg;

/// <summary>
/// Thrown when configuration validation fails for a bound target type.
/// </summary>
public sealed class CfgValidationException : InvalidOperationException
{
    /// <summary>
    /// The type whose configuration validation failed.
    /// </summary>
    public Type TargetType { get; }

    /// <summary>
    /// The validation errors that caused the failure.
    /// </summary>
    public IReadOnlyList<ValidationResult> Errors { get; }

    internal CfgValidationException(Type targetType, List<ValidationResult> errors)
        : base(BuildMessage(targetType, errors))
    {
        TargetType = targetType;
        Errors = errors;
    }

    private static string BuildMessage(Type type, List<ValidationResult> errors) =>
        $"Configuration validation failed for '{type.FullName}':\n"
        + string.Join("\n", errors.Select(e => $"  - {e.ErrorMessage}"));
}
