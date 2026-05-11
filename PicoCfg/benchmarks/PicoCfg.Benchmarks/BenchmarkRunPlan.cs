internal sealed record BenchmarkRunPlan(
    bool IncludeBaselines,
    bool IncludeReload,
    MixedWorkloadScenario? MixedScenario
)
{
    public static bool TryParse(IReadOnlyList<string> args, out BenchmarkRunPlan plan, out string? error)
    {
        var includeBaselines = false;
        var includeReload = false;
        int? mixedN = null;
        int? mixedProviderCount = null;
        int? mixedLookupPassCount = null;

        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--include-baselines":
                    includeBaselines = true;
                    break;

                case "--include-reload":
                    includeReload = true;
                    break;

                case "--mixed-n":
                    if (!TryReadPositiveInt(args, ref i, out var nValue, out error))
                    {
                        plan = Invalid();
                        return false;
                    }

                    mixedN = nValue;
                    break;

                case "--mixed-provider-count":
                    if (!TryReadPositiveInt(args, ref i, out var providerCountValue, out error))
                    {
                        plan = Invalid();
                        return false;
                    }

                    mixedProviderCount = providerCountValue;
                    break;

                case "--mixed-lookup-pass-count":
                    if (!TryReadNonNegativeInt(args, ref i, out var lookupPassCountValue, out error))
                    {
                        plan = Invalid();
                        return false;
                    }

                    mixedLookupPassCount = lookupPassCountValue;
                    break;

                default:
                    error = $"Unknown argument: {args[i]}";
                    plan = Invalid();
                    return false;
            }
        }

        var hasAnyMixedArgument = mixedN.HasValue || mixedProviderCount.HasValue || mixedLookupPassCount.HasValue;
        if (!hasAnyMixedArgument)
        {
            plan = new BenchmarkRunPlan(includeBaselines, includeReload, null);
            error = null;
            return true;
        }

        if (!mixedN.HasValue || !mixedProviderCount.HasValue || !mixedLookupPassCount.HasValue)
        {
            error = "Single mixed scenario mode requires --mixed-n, --mixed-provider-count, and --mixed-lookup-pass-count together.";
            plan = Invalid();
            return false;
        }

        plan = new BenchmarkRunPlan(
            includeBaselines,
            includeReload,
            new MixedWorkloadScenario(mixedN.Value, mixedProviderCount.Value, mixedLookupPassCount.Value)
        );
        error = null;
        return true;
    }

    private static BenchmarkRunPlan Invalid() => new(false, false, null);

    private static bool TryReadPositiveInt(
        IReadOnlyList<string> args,
        ref int index,
        out int value,
        out string? error
    )
    {
        if (!TryReadInt(args, ref index, out value, out error))
            return false;

        if (value <= 0)
        {
            error = $"Expected a positive integer after {args[index - 1]}.";
            return false;
        }

        return true;
    }

    private static bool TryReadNonNegativeInt(
        IReadOnlyList<string> args,
        ref int index,
        out int value,
        out string? error
    )
    {
        if (!TryReadInt(args, ref index, out value, out error))
            return false;

        if (value < 0)
        {
            error = $"Expected a non-negative integer after {args[index - 1]}.";
            return false;
        }

        return true;
    }

    private static bool TryReadInt(
        IReadOnlyList<string> args,
        ref int index,
        out int value,
        out string? error
    )
    {
        value = 0;

        if (index + 1 >= args.Count)
        {
            error = $"Missing value after {args[index]}.";
            return false;
        }

        index++;
        if (int.TryParse(args[index], out value))
        {
            error = null;
            return true;
        }

        error = $"Invalid integer value '{args[index]}'.";
        return false;
    }
}

internal sealed record MixedWorkloadScenario(int N, int ProviderCount, int LookupPassCount)
{
    public override string ToString() =>
        $"N={N}, ProviderCount={ProviderCount}, LookupPassCount={LookupPassCount}";
}
