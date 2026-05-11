namespace PicoCfg.Gen.Tests;

using System.Diagnostics.CodeAnalysis;

public class PicoCfgBindRuntimeTests
{
    [Test]
    public async Task CfgBind_RuntimeLivesInPicoCfgAssembly()
    {
        await Assert.That(typeof(CfgBind).Assembly).IsSameReferenceAs(typeof(Cfg).Assembly);
        await Assert.That(typeof(CfgBindRuntime).Assembly).IsSameReferenceAs(typeof(Cfg).Assembly);
    }

    [Test]
    public async Task Bind_BindsFlatScalarProperties()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Name"] = "PicoCfg",
                    ["Enabled"] = "true",
                    ["Count"] = "42",
                    ["Id"] = "5e5504ea-f1ad-40ef-887f-d8c2421f4189",
                    ["Mode"] = "Advanced",
                    ["Threshold"] = "12.5",
                    ["OptionalCount"] = "7",
                }
            )
            .BuildAsync();

        var model = CfgBind.Bind<FlatSettings>(root);

        await Assert.That(model.Name).IsEqualTo("PicoCfg");
        await Assert.That(model.Enabled).IsTrue();
        await Assert.That(model.Count).IsEqualTo(42);
        await Assert.That(model.Id).IsEqualTo(Guid.Parse("5e5504ea-f1ad-40ef-887f-d8c2421f4189"));
        await Assert.That(model.Mode).IsEqualTo(BindMode.Advanced);
        await Assert.That(model.Threshold).IsEqualTo(12.5m);
        await Assert.That(model.OptionalCount).IsEqualTo(7);
    }

    [Test]
    public async Task Bind_SupportsSectionPrefix()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["App:Name"] = "NestedOnlyByKey",
                    ["App:Enabled"] = "true",
                    ["App:Count"] = "9",
                    ["App:Id"] = "2211c2de-f8fb-45c5-bf09-ac5173cf54a1",
                    ["App:Mode"] = "Basic",
                    ["App:Threshold"] = "1.25",
                }
            )
            .BuildAsync();

        var model = CfgBind.Bind<FlatSettings>(root, section: "App");

        await Assert.That(model.Name).IsEqualTo("NestedOnlyByKey");
        await Assert.That(model.Count).IsEqualTo(9);
        await Assert.That(model.Mode).IsEqualTo(BindMode.Basic);
    }

    [Test]
    public async Task Bind_InvalidConversion_ThrowsFormatException()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Name"] = "Bad",
                    ["Enabled"] = "not-bool",
                }
            )
            .BuildAsync();

        var thrown = await Assert.That(() => CfgBind.Bind<FlatSettings>((ICfg)root)).Throws<FormatException>();
        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown.Message).Contains("Enabled");
    }

    [Test]
    public async Task TryBind_InvalidConversion_ReturnsFalseAndDefaultValue()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Name"] = "Bad",
                    ["Count"] = "nope",
                }
            )
            .BuildAsync();

        var result = CfgBind.TryBind<FlatSettings>(root, out var value);

        await Assert.That(result).IsFalse();
        await Assert.That(value).IsNull();
    }

    [Test]
    public async Task BindInto_OverwritesMatchingPropertiesAndLeavesMissingValuesUntouched()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Name"] = "Updated",
                    ["Count"] = "8",
                    ["Mode"] = "Advanced",
                }
            )
            .BuildAsync();

        var instance = new FlatSettings
        {
            Name = "Before",
            Enabled = true,
            Count = 1,
            Id = Guid.Parse("ca70af5d-1ab9-4c78-89d3-8af51b11db6e"),
            Mode = BindMode.Basic,
            Threshold = 3.5m,
            OptionalCount = 11,
        };

        CfgBind.BindInto((ICfg)root, instance);

        await Assert.That(instance.Name).IsEqualTo("Updated");
        await Assert.That(instance.Count).IsEqualTo(8);
        await Assert.That(instance.Mode).IsEqualTo(BindMode.Advanced);
        await Assert.That(instance.Enabled).IsTrue();
        await Assert.That(instance.Threshold).IsEqualTo(3.5m);
        await Assert.That(instance.OptionalCount).IsEqualTo(11);
    }

    [Test]
    public async Task GeneratedRegistration_WorksForBindIntoOnlyTargetWithoutCtor()
    {
        await using var root = await Cfg
            .CreateBuilder()
            .Add(new Dictionary<string, string> { ["Port"] = "8080" })
            .BuildAsync();

        var instance = CtorlessBindIntoOnly.Create(1);

        CfgBind.BindInto(root, instance);

        await Assert.That(instance.Port).IsEqualTo(8080);
    }

    [Test]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "Trimming",
        "IL2060",
        Justification = "This test intentionally exercises reflection-based generic dispatch to verify the thrown exception shape."
    )]
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "AOT",
        "IL3050",
        Justification = "This test intentionally exercises reflection-based generic dispatch to verify the thrown exception shape."
    )]
    public async Task MissingGeneratedRegistration_FailsFastWithSpecificException()
    {
        await using var root = await Cfg.CreateBuilder().Add(new Dictionary<string, string>()).BuildAsync();

        var method = typeof(CfgBind)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(static method => method.Name == nameof(CfgBind.Bind)
                && method.IsGenericMethodDefinition
                && method.GetParameters() is [{ ParameterType: { Name: nameof(ICfg) } }, ..]);

        var closedMethod = method.MakeGenericMethod(typeof(UnregisteredSettings));

        var thrown = await Assert.That(() => closedMethod.Invoke(null, [root, null])).Throws<TargetInvocationException>();
        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown.InnerException).IsNotNull();
        await Assert.That(thrown.InnerException).IsTypeOf<PicoCfgBindRegistrationException>();
        await Assert.That(thrown.InnerException!.Message).Contains("No generated PicoCfg.Gen registration was found");
    }

    [Test]
    public async Task IncompatibleGeneratedRegistration_FailsFastWithSpecificException()
    {
        CfgBindRuntime.Register<SkewedSettings>(
            contractVersion: CfgBindRuntime.ContractVersion + 1,
            bind: static (_, _) => new SkewedSettings(),
            tryBind: static (ICfg cfg, string? section, [MaybeNullWhen(false)] out SkewedSettings value) =>
            {
                _ = cfg;
                _ = section;
                value = new SkewedSettings();
                return true;
            },
            bindInto: static (_, _, _) => { }
        );

        await using var root = await Cfg.CreateBuilder().Add(new Dictionary<string, string>()).BuildAsync();

        var thrown = await Assert.That(() => CfgBind.Bind<SkewedSettings>(root)).Throws<PicoCfgBindRegistrationException>();
        await Assert.That(thrown).IsNotNull();
        await Assert.That(thrown.Message).Contains("incompatible");
    }

    public sealed class FlatSettings
    {
        public string? Name { get; set; }
        public bool Enabled { get; set; }
        public int Count { get; set; }
        public Guid Id { get; set; }
        public BindMode Mode { get; set; }
        public decimal Threshold { get; set; }
        public int? OptionalCount { get; set; }
    }

    public enum BindMode
    {
        Basic,
        Advanced,
    }

    public sealed class UnregisteredSettings
    {
        public string? Name { get; set; }
    }

    public sealed class SkewedSettings
    {
        public int Value { get; set; }
    }

    [Test]
    public async Task CfgBindRuntime_TryParseDateTime_ValidInputReturnsTrue_InvalidReturnsFalse()
    {
        var valid = CfgBindRuntime.TryParseDateTime("2024-01-15T10:30:00", out var dt);
        await Assert.That(valid).IsTrue();
        await Assert.That(dt).IsEqualTo(new DateTime(2024, 1, 15, 10, 30, 0));

        var invalid = CfgBindRuntime.TryParseDateTime("not-a-date", out var _);
        await Assert.That(invalid).IsFalse();
    }

    [Test]
    public async Task CfgBindRuntime_TryParseDateTimeOffset_ValidInputReturnsTrue_InvalidReturnsFalse()
    {
        var valid = CfgBindRuntime.TryParseDateTimeOffset("2024-01-15T10:30:00+08:00", out var dto);
        await Assert.That(valid).IsTrue();
        await Assert.That(dto).IsEqualTo(new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.FromHours(8)));

        var invalid = CfgBindRuntime.TryParseDateTimeOffset("nope", out var _);
        await Assert.That(invalid).IsFalse();
    }

    [Test]
    public async Task CfgBindRuntime_TryParseDateOnly_ValidInputReturnsTrue_InvalidReturnsFalse()
    {
        var valid = CfgBindRuntime.TryParseDateOnly("2024-01-15", out var d);
        await Assert.That(valid).IsTrue();
        await Assert.That(d).IsEqualTo(new DateOnly(2024, 1, 15));

        var invalid = CfgBindRuntime.TryParseDateOnly("not-a-date", out var _);
        await Assert.That(invalid).IsFalse();
    }

    [Test]
    public async Task CfgBindRuntime_TryParseTimeOnly_ValidInputReturnsTrue_InvalidReturnsFalse()
    {
        var valid = CfgBindRuntime.TryParseTimeOnly("10:30:00", out var t);
        await Assert.That(valid).IsTrue();
        await Assert.That(t).IsEqualTo(new TimeOnly(10, 30, 0));

        var invalid = CfgBindRuntime.TryParseTimeOnly("not-a-time", out var _);
        await Assert.That(invalid).IsFalse();
    }

    [Test]
    public async Task CfgBindRuntime_TryParseTimeSpan_ValidInputReturnsTrue_InvalidReturnsFalse()
    {
        var valid = CfgBindRuntime.TryParseTimeSpan("01:30:00", out var ts);
        await Assert.That(valid).IsTrue();
        await Assert.That(ts).IsEqualTo(new TimeSpan(1, 30, 0));

        var invalid = CfgBindRuntime.TryParseTimeSpan("not-a-span", out var _);
        await Assert.That(invalid).IsFalse();
    }

    [Test]
    public async Task CfgBindRuntime_TryParseUri_ValidInputReturnsTrue_InvalidReturnsFalse()
    {
        var valid = CfgBindRuntime.TryParseUri("https://example.com", out var uri);
        await Assert.That(valid).IsTrue();
        await Assert.That(uri!.AbsoluteUri).IsEqualTo("https://example.com/");

        var invalid = CfgBindRuntime.TryParseUri("", out var _);
        await Assert.That(invalid).IsFalse();
    }

    [Test]
    public async Task CfgBindRuntime_TryParseUri_AcceptsRelativeUri()
    {
        var valid = CfgBindRuntime.TryParseUri("relative/path", out var uri);
        await Assert.That(valid).IsTrue();
        await Assert.That(uri).IsNotNull();
        await Assert.That(uri!.OriginalString).IsEqualTo("relative/path");
    }

    [Test]
    public async Task CfgBindRuntime_TryParseUri_TrimsWhitespace()
    {
        var valid = CfgBindRuntime.TryParseUri(" https://example.com ", out var uri);
        await Assert.That(valid).IsTrue();
        await Assert.That(uri!.AbsoluteUri).IsEqualTo("https://example.com/");
    }

    [Test]
    public async Task CfgBindRuntime_TryParseVersion_ValidInputReturnsTrue_InvalidReturnsFalse()
    {
        var valid = CfgBindRuntime.TryParseVersion("1.2.3", out var v);
        await Assert.That(valid).IsTrue();
        await Assert.That(v).IsEqualTo(new Version(1, 2, 3));

        var invalid = CfgBindRuntime.TryParseVersion("not-a-version", out var _);
        await Assert.That(invalid).IsFalse();
    }

    [Test]
    public async Task CfgBindRuntime_TryParseVersion_TrimsWhitespace()
    {
        var valid = CfgBindRuntime.TryParseVersion(" 1.2.3 ", out var v);
        await Assert.That(valid).IsTrue();
        await Assert.That(v).IsEqualTo(new Version(1, 2, 3));
    }

    [Test]
    public async Task CfgBindRuntime_TryParseBigInteger_ValidInputReturnsTrue_InvalidReturnsFalse()
    {
        var valid = CfgBindRuntime.TryParseBigInteger("12345678901234567890", out var bi);
        await Assert.That(valid).IsTrue();
        await Assert.That(bi).IsEqualTo(System.Numerics.BigInteger.Parse("12345678901234567890"));

        var invalid = CfgBindRuntime.TryParseBigInteger("not-a-number", out var _);
        await Assert.That(invalid).IsFalse();
    }

    public sealed class CtorlessBindIntoOnly
    {
        private CtorlessBindIntoOnly() { }

        public int Port { get; set; }

        public static CtorlessBindIntoOnly Create(int port) => new() { Port = port };
    }
}
