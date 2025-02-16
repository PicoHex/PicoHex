namespace PicoHex.Logger.Json;

public class JsonFormatter(bool indent = false) : ILogFormatter
{
    private readonly JsonSerializerOptions _options =
        new()
        {
            WriteIndented = indent,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new ExceptionConverter() }
        };

    public string Format(LogEntry entry)
    {
        var logObject = new
        {
            Timestamp = entry.Timestamp,
            Level = entry.Level.ToString(),
            Category = entry,
            Message = entry.Message,
            Exception = entry.Exception,
            Scope = entry.Scopes?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };

        return JsonSerializer.Serialize(logObject, _options);
    }
}

internal class ExceptionConverter : JsonConverter<Exception>
{
    public override Exception Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) => throw new NotSupportedException();

    public override void Write(
        Utf8JsonWriter writer,
        Exception value,
        JsonSerializerOptions options
    )
    {
        writer.WriteStartObject();
        writer.WriteString("Type", value.GetType().ToString());
        writer.WriteString("Message", value.Message);
        writer.WriteString("StackTrace", value.StackTrace);
        writer.WriteEndObject();
    }
}
