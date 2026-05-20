namespace PicoCfg.Tests;

public sealed class CfgValidatorTests
{
    [Before(Class)]
    public static void SetupBinding() => CfgBindTestHelper.RegisterValidatableTargetBinding();

    public sealed class ValidatableTarget : IValidatableObject
    {
        public string? Name { get; set; }
        public int Count { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var results = new List<ValidationResult>();

            if (string.IsNullOrWhiteSpace(Name))
                results.Add(new ValidationResult("Name is required.", [nameof(Name)]));

            if (Count < 1 || Count > 100)
                results.Add(
                    new ValidationResult("Count must be between 1 and 100.", [nameof(Count)])
                );

            return results;
        }
    }

    [Test]
    public async Task ValidateOrThrow_ValidInstance_DoesNotThrow()
    {
        var valid = new ValidatableTarget { Name = "Test", Count = 42 };

        await Assert.That(() => CfgValidator.ValidateOrThrow(valid)).ThrowsNothing();
    }

    [Test]
    public async Task ValidateOrThrow_InvalidInstance_Throws()
    {
        var invalid = new ValidatableTarget { Name = null!, Count = 0 };

        var ex = await Assert
            .That(() => CfgValidator.ValidateOrThrow(invalid))
            .Throws<CfgValidationException>();

        await Assert.That(ex!.TargetType).IsEqualTo(typeof(ValidatableTarget));
    }

    [Test]
    public async Task ValidateOrThrow_InvalidInstance_ContainsExpectedErrorCount()
    {
        var invalid = new ValidatableTarget { Name = null!, Count = 0 };

        var ex = await Assert
            .That(() => CfgValidator.ValidateOrThrow(invalid))
            .Throws<CfgValidationException>();

        await Assert.That(ex!.Errors.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Validate_ValidInstance_ReturnsEmptyList()
    {
        var valid = new ValidatableTarget { Name = "Test", Count = 42 };

        var errors = CfgValidator.Validate(valid);

        await Assert.That(errors).IsNotNull();
        await Assert.That(errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_InvalidInstance_ReturnsErrors()
    {
        var invalid = new ValidatableTarget { Name = null!, Count = 0 };

        var errors = CfgValidator.Validate(invalid);

        await Assert.That(errors).IsNotNull();
        await Assert.That(errors).IsNotEmpty();
        await Assert
            .That(errors.Any(e => e.MemberNames.Contains(nameof(ValidatableTarget.Name))))
            .IsTrue();
        await Assert
            .That(errors.Any(e => e.MemberNames.Contains(nameof(ValidatableTarget.Count))))
            .IsTrue();
    }

    [Test]
    public async Task BindAndValidate_BindsAndValidates()
    {
        var builder = Cfg.CreateBuilder();
        builder.Add(
            new Dictionary<string, string>
            {
                ["Validation:Name"] = "Bound",
                ["Validation:Count"] = "50",
            }
        );

        await using var root = await builder.BuildAsync();

        var result = root.BindAndValidate<ValidatableTarget>("Validation");

        await Assert.That(result.Name).IsEqualTo("Bound");
        await Assert.That(result.Count).IsEqualTo(50);
    }

    [Test]
    public async Task BindAndValidate_InvalidBinding_Throws()
    {
        var builder = Cfg.CreateBuilder();
        // Missing the Count key so it stays at 0 (invalid range)
        builder.Add(new Dictionary<string, string> { ["Validation:Name"] = "Bound", });

        await using var root = await builder.BuildAsync();

        await Assert
            .That(() => root.BindAndValidate<ValidatableTarget>("Validation"))
            .Throws<CfgValidationException>();
    }
}
