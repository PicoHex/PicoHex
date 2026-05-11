namespace PicoCfg;

internal static class CfgSnapshotComposer
{
    public static ICfgSnapshot CreateSnapshot(
        IReadOnlyList<ICfgSnapshot> providerSnapshots,
        Func<IReadOnlyDictionary<string, string>, int, CfgSnapshot> snapshotFactory
    )
    {
        return providerSnapshots.Count switch
        {
            0 => CfgSnapshot.Empty,
            1 => providerSnapshots[0],
            _ => CreateMultiProviderSnapshot(providerSnapshots, snapshotFactory),
        };
    }

    public static bool SequenceEqual(
        IReadOnlyList<ICfgSnapshot> left,
        IReadOnlyList<ICfgSnapshot> right
    )
    {
        if (left.Count != right.Count)
            return false;

        for (var i = 0; i < left.Count; i++)
        {
            if (!ReferenceEquals(left[i], right[i]))
                return false;
        }

        return true;
    }

    private static ICfgSnapshot CreateMultiProviderSnapshot(
        IReadOnlyList<ICfgSnapshot> providerSnapshots,
        Func<IReadOnlyDictionary<string, string>, int, CfgSnapshot> snapshotFactory
    )
    {
        // Flatten native snapshots on the reload path so steady-state reads stay on a single dictionary lookup.
        return TryCreateFlattenedSnapshot(providerSnapshots, snapshotFactory, out var snapshot)
            ? snapshot
            : CreateCompositeFallbackSnapshot(providerSnapshots);
    }

    private static bool TryCreateFlattenedSnapshot(
        IReadOnlyList<ICfgSnapshot> providerSnapshots,
        Func<IReadOnlyDictionary<string, string>, int, CfgSnapshot> snapshotFactory,
        out ICfgSnapshot snapshot
    )
    {
        snapshot = CfgSnapshot.Empty;
        var capacity = 0;

        // Validate all snapshots are native CfgSnapshot before allocating the merge array.
        for (var i = 0; i < providerSnapshots.Count; i++)
        {
            if (providerSnapshots[i] is not CfgSnapshot cfgSnapshot)
                return false;

            capacity += cfgSnapshot.Values.Count;
        }

        var dictionaries = new IReadOnlyDictionary<string, string>[providerSnapshots.Count];
        for (var i = 0; i < providerSnapshots.Count; i++)
            dictionaries[i] = ((CfgSnapshot)providerSnapshots[i]).Values;

        var mergedValues = new Dictionary<string, string>(capacity);
        foreach (var t in dictionaries)
        {
            // Merge in provider order so later providers override earlier ones.
            foreach (var (key, value) in t)
                mergedValues[key] = value;
        }

        snapshot = snapshotFactory(
            mergedValues,
            ConfigDataComparer.ComputeFingerprint(mergedValues)
        );
        return true;
    }

    private static ICfgSnapshot CreateCompositeFallbackSnapshot(
        IReadOnlyList<ICfgSnapshot> providerSnapshots
    )
    {
        // Arbitrary ICfgSnapshot implementations can have custom lookup behavior, so fallback preserves
        // provider order and resolves values at read time instead of flattening away that behavior.
        // This keeps custom semantics intact, but steady-state reads become a provider scan rather than
        // the single dictionary lookup used by fully native composed snapshots.
        return new CompositeCfgSnapshot(providerSnapshots);
    }

    internal sealed class CompositeCfgSnapshot(IReadOnlyList<ICfgSnapshot> snapshots) : ICfgSnapshot
    {
        public bool TryGetValue(string path, out string? value)
        {
            for (var i = snapshots.Count - 1; i >= 0; i--)
            {
                if (snapshots[i].TryGetValue(path, out value))
                    return true;
            }

            value = null;
            return false;
        }

        internal IReadOnlyDictionary<string, string> GetAllValues()
        {
            var capacity = 0;
            for (var i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i] is CfgSnapshot cfgSnapshot)
                    capacity += cfgSnapshot.Values.Count;
            }

            var merged = new Dictionary<string, string>(capacity);
            for (var i = 0; i < snapshots.Count; i++)
            {
                if (snapshots[i] is CfgSnapshot cfgSnapshot)
                {
                    foreach (var (key, value) in cfgSnapshot.Values)
                        merged[key] = value;
                }
            }

            return merged;
        }
    }
}
