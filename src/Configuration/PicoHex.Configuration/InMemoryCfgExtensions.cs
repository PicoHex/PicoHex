namespace PicoHex.Configuration;

public static class InMemoryCfgExtensions
{
    public static ICfgBuilder AddInMemoryString(
        this ICfgBuilder builder,
        string configContent,
        Encoding? encoding = null
    )
    {
        encoding ??= Encoding.UTF8;
        return builder.AddStream(() =>
        {
            var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, encoding, leaveOpen: true);
            writer.Write(configContent);
            writer.Flush();
            stream.Position = 0;
            return stream;
        });
    }

    public static ICfgBuilder AddInMemoryDictionary(
        this ICfgBuilder builder,
        IDictionary<string, string> configData
    )
    {
        var configContent = string.Join("\n", configData.Select(kv => $"{kv.Key}={kv.Value}"));
        return builder.AddInMemoryString(configContent);
    }

    public static ICfgBuilder AddInMemoryStream(
        this ICfgBuilder builder,
        Action<Stream> writeAction
    )
    {
        return builder.AddStream(() =>
        {
            var stream = new MemoryStream();
            writeAction(stream);
            stream.Position = 0;
            return stream;
        });
    }
}
