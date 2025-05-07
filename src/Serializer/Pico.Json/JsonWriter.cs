namespace Pico.Json;

internal class JsonWriter
{
    private readonly StringBuilder _sb = new();

    public void BeginObject() => _sb.Append('{');

    public void EndObject() => _sb.Append('}');

    public void WriteProperty(string name, object value)
    {
        _sb.Append($"\"{name}\":{value},");
    }

    public override string ToString() => _sb.ToString().TrimEnd(',') + "}";
}
