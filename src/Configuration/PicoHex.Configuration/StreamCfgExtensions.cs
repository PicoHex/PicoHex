namespace PicoHex.Configuration;

public static class StreamCfgExtensions
{
    public static ICfgBuilder AddStream(this ICfgBuilder builder, Func<Stream> streamFactory)
    {
        return builder.AddSource(new StreamCfgSource(streamFactory));
    }
}
