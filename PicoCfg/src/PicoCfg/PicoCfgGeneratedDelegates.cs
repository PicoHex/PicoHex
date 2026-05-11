namespace PicoCfg;

/// <summary>Delegate for source-generated try-bind operations that do not throw on failure.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate bool PicoCfgGeneratedTryBindDelegate<T>(
    ICfg cfg,
    string? section,
    [MaybeNullWhen(false)] out T value
);

/// <summary>Delegate for source-generated bind-into operations that populate an existing instance.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate void PicoCfgGeneratedBindIntoDelegate<in T>(ICfg cfg, string? section, T instance);
