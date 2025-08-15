namespace Pico.Cfg.Extensions;

public static class StreamCfgExtensions
{
    public static ICfgBuilder Add(this ICfgBuilder builder, Func<Stream> streamFactory) =>
        builder.AddSource(new StreamCfgSource(streamFactory));

    public static ICfgBuilder Add(
        this ICfgBuilder builder,
        string configContent,
        Encoding? encoding = null
    ) =>
        builder.Add(() =>
        {
            var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, encoding ?? Encoding.UTF8, leaveOpen: true);
            writer.Write(configContent);
            writer.Flush();
            stream.Position = 0;
            return stream;
        });

    public static ICfgBuilder Add(
        this ICfgBuilder builder,
        IDictionary<string, string> configData
    ) => builder.Add(string.Join("\n", configData.Select(kv => $"{kv.Key}={kv.Value}")));
}
