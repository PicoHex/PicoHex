namespace PicoHex.Logger.Console;

public class ConsoleFormatter : ILogFormatter
{
    public string Format(LogEntry entry)
    {
        var sb = new StringBuilder();
        sb.Append($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] ");
        sb.Append($"{entry.Level.ToString().ToUpper(), -12}");
        sb.Append($"[{entry.Category}] ");
        sb.Append(entry.Message);

        if (entry.Exception != null)
        {
            sb.AppendLine();
            sb.Append($"EXCEPTION: {entry.Exception}");
        }

        if (entry.Scopes is null || !entry.Scopes.Any())
            return sb.ToString();

        sb.AppendLine();
        sb.Append("SCOPES: [");
        sb.Append(string.Join(" => ", entry.Scopes.Select(s => $"{s.Key}={s.Value}")));
        sb.Append(']');

        return sb.ToString();
    }
}
