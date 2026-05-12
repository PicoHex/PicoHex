namespace PicoLog;

internal static class ConsoleSinkWriter
{
    private static readonly ConditionalWeakTable<TextWriter, object> WriterLocks = new();

    private static object GetLock(TextWriter writer) =>
        WriterLocks.GetValue(writer, static _ => new object());

    public static void Write(TextWriter writer, string message)
    {
        lock (GetLock(writer))
            writer.WriteLine(message);
    }

    public static void Write<TState>(
        TextWriter writer,
        string message,
        TState state,
        Action<TextWriter, string, TState> consoleWrite
    )
    {
        lock (GetLock(writer))
        {
            if (!ReferenceEquals(writer, Console.Out))
            {
                writer.WriteLine(message);
                return;
            }

            consoleWrite(writer, message, state);
        }
    }
}
