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

        var scope = entry.Scope is not null
            ? $" [Scopes: {string.Join(", ", entry.Scope.Select(s => $"{s.Key}={s.Value}"))}]"
            : "";

        sb.AppendLine();
        sb.Append("SCOPES: [");
        sb.Append(string.Join(" => ", scope));
        sb.Append(']');

        return sb.ToString();
    }
}
