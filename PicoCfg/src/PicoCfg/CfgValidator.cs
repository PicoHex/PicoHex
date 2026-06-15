namespace PicoCfg;

/// <summary>
/// Provides validation extensions for configuration-bound types.
/// </summary>
public static class CfgValidator
{
    /// <summary>
    /// Validates <paramref name="instance"/> using <see cref="IValidatableObject"/>.
    /// Returns a list of <see cref="ValidationResult"/> errors, or an empty list when validation succeeds.
    /// This method is not trim-safe because it uses <see cref="ValidationContext"/> which requires reflection.
    /// </summary>
    /// <typeparam name="T">The type to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <returns>A list of validation errors, or an empty list when the instance is valid.</returns>
    [RequiresUnreferencedCode("ValidationContext requires reflection and is not compatible with trimming")]
    public static List<ValidationResult> Validate<T>(T instance)
    {
#if !PICOCFG_NO_VALIDATION
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
#else
        return [];
#endif
    }

    /// <summary>
    /// Validates <paramref name="instance"/> using <see cref="IValidatableObject"/>
    /// and throws <see cref="CfgValidationException"/> when validation fails.
    /// </summary>
    /// <typeparam name="T">The type to validate.</typeparam>
    /// <param name="instance">The instance to validate.</param>
    /// <exception cref="CfgValidationException">Thrown when validation produces one or more errors.</exception>
    [RequiresUnreferencedCode("ValidationContext requires reflection and is not compatible with trimming")]
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
    /// Fully AOT-compatible: uses source-generated binding and <see cref="IValidatableObject"/>,
    /// neither of which require runtime reflection.
    /// </summary>
    /// <typeparam name="T">The type to bind and validate.</typeparam>
    /// <param name="cfg">The configuration root.</param>
    /// <param name="section">Optional configuration section key prefix.</param>
    /// <returns>The bound and validated instance of <typeparamref name="T"/>.</returns>
    [RequiresUnreferencedCode("ValidationContext requires reflection and is not compatible with trimming")]
    public static T BindAndValidate<
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicConstructors
                | DynamicallyAccessedMemberTypes.PublicProperties
        )]
            T
    >(this ICfg cfg, string? section = null)
    {
        var instance = CfgBind.Bind<T>(cfg, section);
        CfgValidator.ValidateOrThrow(instance);
        return instance;
    }
}
