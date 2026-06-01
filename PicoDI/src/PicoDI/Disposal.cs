namespace PicoDI;

/// <summary>
/// Disposal guards and helpers for SvcContainer/SvcScope.
/// </summary>
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

    public static async ValueTask DisposeInstanceAsync(object instance, HashSet<object> disposed)
    {
        if (!disposed.Add(instance))
            return;
        switch (instance)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
