namespace PicoCfg.Extensions;

/// <summary>
/// Convenience methods for adding common configuration source types to a <see cref="CfgBuilder"/>.
/// </summary>
/// <remarks>
/// The source types supported by these extensions are unified by their backing store:
/// <list type="bullet">
///   <item><description><b>Stream-based</b> — inline text, file stream, or any <see cref="Stream"/> factory (<see cref="CfgBuilderExtensions.Add(CfgBuilder, Func{Stream}, Encoding?, Func{object?}?)"/>).</description></item>
///   <item><description><b>Dictionary-based</b> — in-memory <see cref="IDictionary{TKey, TValue}"/> or factory delegate (<see cref="CfgBuilderExtensions.Add(CfgBuilder, IDictionary{string, string}, Func{object?}?)"/>).</description></item>
///   <item><description><b>Environment variables</b> — OS environment with optional prefix filtering (<see cref="CfgBuilderExtensions.AddEnvironmentVariables(CfgBuilder, string?)"/>).</description></item>
///   <item><description><b>Command-line arguments</b> — structured CLI input (<see cref="CfgBuilderExtensions.AddCommandLine(CfgBuilder, string[], string?)"/>).</description></item>
///   <item><description><b>Key-per-file</b> — Kubernetes ConfigMap style for container environments (<see cref="CfgBuilderExtensions.AddKeyPerFile(CfgBuilder, string, Func{string, bool}?)"/>).</description></item>
///   <item><description><b>Chained</b> — delegate to another <see cref="ICfg"/> instance (<see cref="CfgBuilderExtensions.AddConfiguration(CfgBuilder, ICfg)"/>).</description></item>
/// </list>
/// All source types produce <see cref="Dictionary{TKey, TValue}"/> snapshots keyed by configuration path
/// (<c>Section:Key</c> format). Providers are additive: later sources override earlier ones on key conflict.
/// </remarks>
public static class CfgBuilderExtensions
{
    extension(CfgBuilder builder)
    {
        /// <summary>
        /// Adds a stream-based source.
        /// The stream content is parsed as line-based <c>key=value</c> text on each reload.
        /// When <paramref name="versionStampFactory"/> is provided, equal consecutive stamps are treated
        /// as authoritative unchanged signals for future reloads after the first completed materialization.
        /// Once a stamp has been accepted, repeated equal values, including <see langword="null"/>, skip
        /// reopening the stream. A changed stamp triggers a reread, but the current snapshot may still be
        /// retained when the reparsed content is unchanged.
        /// </summary>
        public CfgBuilder Add(Func<CancellationToken, ValueTask<Stream>> streamFactory,
            Encoding? encoding = null,
            Func<object?>? versionStampFactory = null
        ) =>
            builder.AddSource(builder.CreateStreamSource(streamFactory, versionStampFactory, encoding));

        /// <summary>
        /// Adds a stream-based source with file-change auto-reload.
        /// The stream provider is decorated with a <see cref="FileWatchingCfgProvider"/> that monitors
        /// <paramref name="watchPath"/> and triggers reloads on change events with a debounce interval.
        /// </summary>
        public CfgBuilder Add(Func<CancellationToken, ValueTask<Stream>> streamFactory,
            string watchPath,
            Encoding? encoding = null,
            Func<object?>? versionStampFactory = null
        ) =>
            builder.AddSource(
                builder.CreateStreamSource(
                    streamFactory,
                    watchPath,
                    versionStampFactory,
                    encoding
                )
            );

        /// <summary>
        /// Adds inline text content as a stream-based source.
        /// The content is parsed as line-based <c>key=value</c> text.
        /// Each reload recreates a stream so inline text stays on the same parsing path as stream-based sources.
        /// When <paramref name="versionStampFactory"/> is provided, equal consecutive stamps are treated
        /// as authoritative unchanged signals for future reloads after the first completed materialization.
        /// Once a stamp has been accepted, repeated equal values, including <see langword="null"/>, skip
        /// reparsing the inline content. A changed stamp triggers reparsing, but the current snapshot may still
        /// be retained when the reparsed content is unchanged.
        /// </summary>
        public CfgBuilder Add(string configContent,
            Encoding? encoding = null,
            Func<object?>? versionStampFactory = null
        ) =>
            builder.AddSource(
                builder.CreateStreamSource(
                    ct =>
                    {
                        var stream = new MemoryStream();
                        using var writer = new StreamWriter(
                            stream,
                            encoding ?? Encoding.UTF8,
                            leaveOpen: true
                        );
                        writer.Write(configContent);
                        writer.Flush();
                        stream.Position = 0;
                        return ValueTask.FromResult<Stream>(stream);
                    },
                    versionStampFactory
                )
            );

        /// <summary>
        /// Adds an in-memory dictionary source.
        /// Dictionary values are used as-is and are not reparsed as <c>key=value</c> text.
        /// When <paramref name="versionStampFactory"/> is provided, equal consecutive stamps are treated
        /// as authoritative unchanged signals for future reloads after the first completed materialization.
        /// Once a stamp has been accepted, repeated equal values, including <see langword="null"/>, skip
        /// rereading the dictionary. A changed stamp triggers reread, but the current snapshot may still be
        /// retained when the visible dictionary content is unchanged.
        /// </summary>
        public CfgBuilder Add(IDictionary<string, string> configData,
            Func<object?>? versionStampFactory = null
        ) => builder.AddSource(builder.CreateDictionarySource(configData, versionStampFactory));

        /// <summary>
        /// Adds a dictionary-backed factory source.
        /// Each reload enumerates the current key/value pairs unless <paramref name="versionStampFactory"/>
        /// returns the same value as the previously accepted authoritative stamp.
        /// The first completed materialization establishes that baseline; later equal values, including
        /// repeated <see langword="null"/>, skip source work, while changed stamps force re-enumeration even
        /// if the current snapshot instance is ultimately retained.
        /// </summary>
        public CfgBuilder Add(
            Func<IEnumerable<KeyValuePair<string, string>>> dataFactory,
            Func<object?>? versionStampFactory = null
        ) => builder.AddSource(builder.CreateDictionarySource(dataFactory, versionStampFactory));

        /// <summary>
        /// Adds an environment-variable source.
        /// When <paramref name="prefix"/> is provided, only variables whose names start with the prefix
        /// are included and the prefix is stripped from the resulting key.
        /// Double underscores (<c>__</c>) in variable names are mapped to <c>:</c>.
        /// </summary>
        public CfgBuilder AddEnvironmentVariables(string? prefix = null) =>
            builder.AddSource(
                new EnvCfgSource(() => new EnvCfgProvider(prefix, builder.CreateProviderState())
                )
            );

        /// <summary>
        /// Adds a command-line argument source.
        /// Supports <c>--key=value</c>, <c>--key value</c>, <c>-key value</c>, and <c>/key value</c> formats.
        /// Arguments without a value are treated as switches with value <c>"true"</c>.
        /// When <paramref name="prefix"/> is provided, only arguments whose key starts with the prefix
        /// are included and the prefix is stripped from the resulting key.
        /// </summary>
        public CfgBuilder AddCommandLine(string[] args, string? prefix = null) =>
            builder.AddSource(
                new CmdLineCfgSource(() => new CmdLineCfgProvider(args, prefix, builder.CreateProviderState())
                )
            );

        /// <summary>
        /// Adds a key-per-file configuration source.
        /// Each file in the specified directory becomes a configuration entry where the filename
        /// (including extension) is the key and the file content (UTF-8) is the value.
        /// Files starting with <c>.</c> are skipped by default.
        /// Subdirectories are not recursed into.
        /// An empty or missing directory produces an empty configuration set.
        /// </summary>
        public CfgBuilder AddKeyPerFile(string directoryPath, Func<string, bool>? keyFilter = null) =>
            builder.AddSource(
                new KeyPerFileCfgSource(() =>
                    new KeyPerFileCfgProvider(directoryPath, keyFilter, builder.CreateProviderState())
                )
            );

        /// <summary>
        /// Chains an existing <see cref="ICfg"/> as a configuration source.
        /// Values are read live from the chained config on each snapshot read.
        /// On reload, the chained config is enumerated via <see cref="CfgEnumerationExtensions.GetAll(ICfg)"/>
        /// to detect changes and signal change notifications.
        /// The caller is responsible for ensuring the chained config does not introduce cycles.
        /// </summary>
        public CfgBuilder AddConfiguration(ICfg chainedConfig) =>
            builder.AddSource(
                new ChainedCfgSource(() => new ChainedCfgProvider(chainedConfig, builder.CreateProviderState())
                )
            );
    }
}
