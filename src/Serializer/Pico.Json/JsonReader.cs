namespace Pico.Json;

internal class JsonReader
{
    private readonly string _json;
    private int _position;

    public JsonReader(string json) => _json = json;

    public string CurrentProperty { get; private set; }

    public bool ReadNextProperty()
    {
        // 简化实现：实际需要解析JSON结构
        return false;
    }
}
