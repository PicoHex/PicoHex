using PicoCfg;
using PicoCfg.Abs;

namespace PicoCfg.Gen.Tests;

// BUG-REPORT 2026.8.7 follow-up: runtime verification for Issue A (positional
// records with primary constructors — including IReadOnlyList<T> members) and
// Issue C (deep dictionary value binding with the reported key shapes).
public sealed class PicoCfgFollowUpRuntimeTests
{
    // --- Issue A: positional records (primary constructor) ---

    public sealed record ModelCost(decimal Input, decimal Output);

    public sealed record ModelCostTier(string Name, decimal Price);

    public sealed record PricingRow(ModelCost Base, IReadOnlyList<ModelCostTier>? Tiers);

    public sealed record RegistryConfig
    {
        public Dictionary<string, PricingRow> Models { get; init; } = new();
    }

    [Test]
    public async Task Bind_PositionalRecordWithReadOnlyListMember_BindsValues()
    {
        await using var root = await Cfg.CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Models:0:Key"] = "m1",
                    ["Models:0:Value:Base:Input"] = "1.5",
                    ["Models:0:Value:Base:Output"] = "2.5",
                    ["Models:0:Value:Tiers:0:Name"] = "t1",
                    ["Models:0:Value:Tiers:0:Price"] = "3.5",
                    ["Models:0:Value:Tiers:1:Name"] = "t2",
                    ["Models:0:Value:Tiers:1:Price"] = "4.5",
                }
            )
            .BuildAsync();

        var config = CfgBind.Bind<RegistryConfig>(root);

        await Assert.That(config.Models).IsNotNull();
        await Assert.That(config.Models.Count).IsEqualTo(1);
        var row = config.Models["m1"];
        await Assert.That(row.Base.Input).IsEqualTo(1.5m);
        await Assert.That(row.Base.Output).IsEqualTo(2.5m);
        await Assert.That(row.Tiers).IsNotNull();
        await Assert.That(row.Tiers!.Count).IsEqualTo(2);
        await Assert.That(row.Tiers![0].Name).IsEqualTo("t1");
        await Assert.That(row.Tiers![0].Price).IsEqualTo(3.5m);
        await Assert.That(row.Tiers![1].Name).IsEqualTo("t2");
        await Assert.That(row.Tiers![1].Price).IsEqualTo(4.5m);
    }

    [Test]
    public async Task Bind_PositionalRecordAsTopLevelTarget_BindsValues()
    {
        await using var root = await Cfg.CreateBuilder()
            .Add(new Dictionary<string, string> { ["Input"] = "10", ["Output"] = "20" })
            .BuildAsync();

        var cost = CfgBind.Bind<ModelCost>(root);

        await Assert.That(cost.Input).IsEqualTo(10m);
        await Assert.That(cost.Output).IsEqualTo(20m);
    }

    [Test]
    public async Task TryBind_PositionalRecord_BindsValues()
    {
        await using var root = await Cfg.CreateBuilder()
            .Add(new Dictionary<string, string> { ["Name"] = "t1", ["Price"] = "9.99" })
            .BuildAsync();

        var ok = CfgBind.TryBind<ModelCostTier>(root, out var tier);

        await Assert.That(ok).IsTrue();
        await Assert.That(tier!.Name).IsEqualTo("t1");
        await Assert.That(tier!.Price).IsEqualTo(9.99m);
    }

    public sealed record RecordWithBodyProperty(int Port)
    {
        public string? Name { get; init; }
    }

    [Test]
    public async Task Bind_PositionalRecordWithBodyProperty_BindsBoth()
    {
        await using var root = await Cfg.CreateBuilder()
            .Add(new Dictionary<string, string> { ["Port"] = "8080", ["Name"] = "main" })
            .BuildAsync();

        var settings = CfgBind.Bind<RecordWithBodyProperty>(root);

        await Assert.That(settings.Port).IsEqualTo(8080);
        await Assert.That(settings.Name).IsEqualTo("main");
    }

    [Test]
    public async Task Bind_PositionalRecordUnderSection_BindsValues()
    {
        await using var root = await Cfg.CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Pricing:Base:Input"] = "1.5",
                    ["Pricing:Base:Output"] = "2.5",
                    ["Pricing:Tiers:0:Name"] = "t1",
                    ["Pricing:Tiers:0:Price"] = "3.5",
                }
            )
            .BuildAsync();

        var row = CfgBind.Bind<PricingRow>(root, "Pricing");

        await Assert.That(row.Base.Input).IsEqualTo(1.5m);
        await Assert.That(row.Base.Output).IsEqualTo(2.5m);
        await Assert.That(row.Tiers).IsNotNull();
        await Assert.That(row.Tiers!.Count).IsEqualTo(1);
        await Assert.That(row.Tiers![0].Name).IsEqualTo("t1");
    }

    // --- Issue C: deep dictionary value types with the reported key shapes ---

    public sealed class DeepModelCost
    {
        public decimal Input { get; set; }
        public decimal Output { get; set; }
    }

    public sealed class DeepModelCostTier
    {
        public string? Name { get; set; }
        public DeepModelCost Cost { get; set; } = new();
    }

    public sealed class DeepPricingRow
    {
        public DeepModelCost Base { get; set; } = new();
        public IReadOnlyList<DeepModelCostTier>? Tiers { get; set; }
    }

    public sealed class DeepRegistryConfig
    {
        public Dictionary<string, DeepPricingRow> Models { get; set; } = new();
    }

    [Test]
    public async Task Bind_DeepDictionaryValueType_WithReportedKeyShapes_BindsFully()
    {
        // The report's exact key shapes: models:0:Value:Base:Input (5 segments)
        // and models:0:Value:Tiers:0:Cost:Input (7 segments) under a section.
        await using var root = await Cfg.CreateBuilder()
            .Add(
                new Dictionary<string, string>
                {
                    ["Pricing:models:0:Key"] = "m1",
                    ["Pricing:models:0:Value:Base:Input"] = "1.5",
                    ["Pricing:models:0:Value:Base:Output"] = "2.5",
                    ["Pricing:models:0:Value:Tiers:0:Name"] = "t1",
                    ["Pricing:models:0:Value:Tiers:0:Cost:Input"] = "3.5",
                    ["Pricing:models:0:Value:Tiers:0:Cost:Output"] = "4.5",
                    ["Pricing:models:0:Value:Tiers:1:Name"] = "t2",
                    ["Pricing:models:0:Value:Tiers:1:Cost:Input"] = "5.5",
                    ["Pricing:models:0:Value:Tiers:1:Cost:Output"] = "6.5",
                }
            )
            .BuildAsync();

        var config = CfgBind.Bind<DeepRegistryConfig>(root, "Pricing");

        await Assert.That(config.Models).IsNotNull();
        await Assert.That(config.Models.Count).IsEqualTo(1);
        var row = config.Models["m1"];
        await Assert.That(row.Base.Input).IsEqualTo(1.5m);
        await Assert.That(row.Base.Output).IsEqualTo(2.5m);
        await Assert.That(row.Tiers).IsNotNull();
        await Assert.That(row.Tiers!.Count).IsEqualTo(2);
        await Assert.That(row.Tiers![0].Name).IsEqualTo("t1");
        await Assert.That(row.Tiers![0].Cost.Input).IsEqualTo(3.5m);
        await Assert.That(row.Tiers![0].Cost.Output).IsEqualTo(4.5m);
        await Assert.That(row.Tiers![1].Name).IsEqualTo("t2");
        await Assert.That(row.Tiers![1].Cost.Input).IsEqualTo(5.5m);
        await Assert.That(row.Tiers![1].Cost.Output).IsEqualTo(6.5m);
    }
}
