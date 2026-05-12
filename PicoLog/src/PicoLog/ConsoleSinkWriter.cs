using System.Runtime.CompilerServices;

namespace PicoLog;

internal static class ConsoleSinkWriter
{
    private static readonly ConditionalWeakTable<TextWriter, object> WriterLocks = new();

    private static object GetLock(TextWriter writer) =>
        WriterLocks.GetValue(writer, static _ => new object());

    public static Task WriteAsync(TextWriter writer, string message)
    {
        lock (GetLock(writer))
            writer.WriteLine(message);

        return Task.CompletedTask;
    }

    public static Task WriteAsync<TState>(
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
                return Task.CompletedTask;
            }

            consoleWrite(writer, message, state);
        }

        return Task.CompletedTask;
    }
}
