namespace Pico.Json
{
    public static partial class Serializer
    {
        // 同步方法
        public static string Serialize<T>(T value) => Generated.JsonSerializer<T>.Serialize(value);

        public static byte[] SerializeToUtf8Bytes<T>(T value) =>
            Encoding.UTF8.GetBytes(Serialize(value));

        public static T Deserialize<T>(string json) =>
            Generated.JsonSerializer<T>.Deserialize(json);

        public static T DeserializeFromUtf8Bytes<T>(byte[] bytes) =>
            Deserialize<T>(Encoding.UTF8.GetString(bytes));

        // 流同步操作
        public static void Serialize<T>(Stream stream, T value)
        {
            byte[] bytes = SerializeToUtf8Bytes(value);
            stream.Write(bytes, 0, bytes.Length);
        }

        public static T Deserialize<T>(Stream stream)
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return DeserializeFromUtf8Bytes<T>(ms.ToArray());
        }

        // 流异步操作（通过扩展类实现）
    }
}
