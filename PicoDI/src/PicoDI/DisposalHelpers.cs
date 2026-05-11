namespace PicoDI;

internal static class DisposalHelpers
{
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
