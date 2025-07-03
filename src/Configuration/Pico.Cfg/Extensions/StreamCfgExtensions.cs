namespace Pico.CFG.Extensions;

public static class StreamCFGExtensions
{
    public static ICFGBuilder Add(this ICFGBuilder builder, Func<Stream> streamFactory) =>
        builder.AddSource(new StreamCFGSource(streamFactory));

    public static ICFGBuilder Add(
        this ICFGBuilder builder,
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

    public static ICFGBuilder Add(
        this ICFGBuilder builder,
        IDictionary<string, string> configData
    ) => builder.Add(string.Join("\n", configData.Select(kv => $"{kv.Key}={kv.Value}")));
}
