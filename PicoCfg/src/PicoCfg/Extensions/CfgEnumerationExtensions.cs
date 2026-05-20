namespace PicoCfg.Extensions;

/// <summary>
/// Extension methods for enumerating configuration key-value pairs from <see cref="ICfg"/> views.
/// </summary>
public static class CfgEnumerationExtensions
{
    /// <summary>
    /// Returns all key-value pairs from the configuration view.
    /// When the underlying snapshot is a native PicoCfg snapshot (single or composed), the returned
    /// dictionary contains all keys merged in provider order — later providers override earlier ones.
    /// External <see cref="ICfg"/> implementations return an empty dictionary.
    /// </summary>
    public static IReadOnlyDictionary<string, string> GetAll(this ICfg cfg)
    {
        if (cfg is CfgSnapshot snapshot)
            return snapshot.GetAllValues();

        if (cfg is CfgSnapshotComposer.CompositeCfgSnapshot composite)
            return composite.GetAllValues();

        if (cfg is IInternalCfgRootSnapshotAccessor rootAccessor)
        {
            if (rootAccessor.CurrentSnapshot is CfgSnapshot rootSnapshot)
                return rootSnapshot.GetAllValues();

            if (
                rootAccessor.CurrentSnapshot
                is CfgSnapshotComposer.CompositeCfgSnapshot rootComposite
            )
                return rootComposite.GetAllValues();
        }

        if (cfg is CfgSection section)
        {
            var prefix = section.Path;
            if (string.IsNullOrEmpty(prefix))
                return section.Parent.GetAll();

            var parentAll = section.Parent.GetAll();
            var filtered = new Dictionary<string, string>(parentAll.Count);
            var searchPrefix = prefix + ":";

            foreach (var kvp in parentAll)
            {
                if (kvp.Key.StartsWith(searchPrefix, StringComparison.Ordinal))
                    filtered[kvp.Key[searchPrefix.Length..]] = kvp.Value;
            }

            return filtered;
        }

        return new Dictionary<string, string>();
    }
}
