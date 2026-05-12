namespace PicoCfg;

[RequiresUnreferencedCode("CfgValidator uses IValidatableObject-based validation. Types implementing IValidatableObject are compatible with Native AOT. DataAnnotations-based validation is not supported under trimming.")]
public static class CfgValidator
{
    public static List<ValidationResult> Validate<T>(T instance)
    {
        var results = new List<ValidationResult>();
        if (instance is IValidatableObject validatable)
        {
            var context = new ValidationContext(instance!, null, null);
            var validationResults = validatable.Validate(context);
            if (validationResults is not null)
            {
                foreach (var r in validationResults)
                {
                    if (r is not null && r != ValidationResult.Success)
                        results.Add(r);
                }
            }
        }
        return results;
    }

    public static void ValidateOrThrow<T>(T instance)
    {
        var errors = Validate(instance);
        if (errors.Count > 0)
            throw new CfgValidationException(typeof(T), errors);
    }
}

public static class CfgValidationExtensions
{
    /// <summary>
    /// Binds configuration to type <typeparamref name="T"/> and validates the result.
    /// </summary>
    /// <typeparam name="T">The type to bind and validate.</typeparam>
    /// <param name="cfg">The configuration root.</param>
    /// <param name="section">Optional configuration section key prefix.</param>
    /// <returns>The bound and validated instance of <typeparamref name="T"/>.</returns>
    [RequiresUnreferencedCode(
        "CfgValidator uses IValidatableObject-based validation. "
        + "Types implementing IValidatableObject are compatible with Native AOT. "
        + "DataAnnotations-based validation is not supported under trimming."
    )]
    public static T BindAndValidate<T>(this ICfg cfg, string? section = null)
    {
        var instance = CfgBind.Bind<T>(cfg, section);
        CfgValidator.ValidateOrThrow(instance);
        return instance;
    }
}
