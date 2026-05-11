namespace PicoCfg;

public static class CfgValidator
{
    public static List<ValidationResult> Validate<T>(T instance)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(instance!, null, null);
        Validator.TryValidateObject(instance!, context, results, validateAllProperties: true);
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
    public static T BindAndValidate<T>(this ICfg cfg, string? section = null)
    {
        var instance = CfgBind.Bind<T>(cfg, section);
        CfgValidator.ValidateOrThrow(instance);
        return instance;
    }
}
