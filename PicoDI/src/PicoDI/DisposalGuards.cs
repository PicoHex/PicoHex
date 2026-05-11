namespace PicoDI;

internal static class DisposalGuards
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfDisposed(ref int disposedFlag, string typeName)
    {
        if (Volatile.Read(ref disposedFlag) != 0)
            ThrowObjectDisposedException(typeName);
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowObjectDisposedException(string typeName) =>
        throw new ObjectDisposedException(typeName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void LogDisposeError(object? instance, Exception exception)
    {
        Trace.WriteLine(
            $"Error disposing service instance of type '{instance?.GetType().FullName ?? "unknown"}': {exception}"
        );
    }
}
