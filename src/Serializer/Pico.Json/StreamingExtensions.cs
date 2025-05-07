namespace Pico.Json
{
    public static class StreamingExtensions
    {
        public static async Task SerializeAsync<T>(
            this Stream stream,
            T value,
            CancellationToken cancellationToken = default
        )
        {
            byte[] bytes = Serializer.SerializeToUtf8Bytes(value);
            await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
        }

        public static async Task<T> DeserializeAsync<T>(
            this Stream stream,
            CancellationToken cancellationToken = default
        )
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, 81920, cancellationToken);
            return Serializer.DeserializeFromUtf8Bytes<T>(ms.ToArray());
        }
    }
}
