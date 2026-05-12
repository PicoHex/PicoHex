namespace PicoDI;

public sealed partial class SvcScope
{
    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        DetachFromParent();
        await DisposeChildScopesAsync();
        await DisposeScopedInstancesAsync();
        await DisposeTransientInstancesAsync();
    }

    private async ValueTask DisposeChildScopesAsync()
    {
        foreach (var child in DetachAllChildScopes())
        {
            try
            {
                await child.DisposeAsync();
            }
            catch (Exception ex)
            {
                OwningContainer?.OnError?.Invoke(
                    ex,
                    "Error disposing child scope asynchronously"
                );
            }
        }
    }

    private async ValueTask DisposeScopedInstancesAsync()
    {
        var instances = Interlocked.Exchange(ref _scopedInstances, null);
        var creationOrder = Interlocked.Exchange(ref _scopedCreationOrder, null);

        if (instances is null)
            return;

        // Sort by descending creation order (LIFO) so a scoped service can
        // safely use its dependencies in its own Dispose method.
        List<(long Order, object Instance)>? ordered = null;
        if (creationOrder is not null)
        {
            ordered = creationOrder.ToList();
            ordered.Sort(static (a, b) => b.Order.CompareTo(a.Order));
        }

        var disposedInstances = new HashSet<object>();

        if (ordered is not null)
        {
            foreach (var (_, svc) in ordered)
            {
                try
                {
                    await DisposalHelpers.DisposeInstanceAsync(svc, disposedInstances);
                }
                catch (Exception ex)
                {
                    OwningContainer?.OnError?.Invoke(
                        ex,
                        $"Error disposing service instance of type '{svc.GetType().FullName}'"
                    );
                }
            }
        }

        // Dispose any remaining instances not captured by the creation-order
        // queue (defensive — should be empty when the queue is present).
        foreach (var lazy in instances.Values)
        {
            object? svc;
            try
            {
                svc = lazy.Value;
            }
            catch
            {
                continue;
            }

            if (!disposedInstances.Add(svc))
                continue;

            try
            {
                await DisposalHelpers.DisposeInstanceAsync(svc, disposedInstances);
            }
            catch (Exception ex)
            {
                OwningContainer?.OnError?.Invoke(
                    ex,
                    $"Error disposing service instance of type '{svc?.GetType().FullName ?? "unknown"}'"
                );
            }
        }

        instances.Clear();
    }

    private async ValueTask DisposeTransientInstancesAsync()
    {
        var queue = Interlocked.Exchange(ref _trackedTransients, null);
        if (queue is null)
            return;

        var disposedInstances = new HashSet<object>();
        while (queue.TryDequeue(out var instance))
        {
            try
            {
                await DisposalHelpers.DisposeInstanceAsync(instance, disposedInstances);
            }
            catch (Exception ex)
            {
                OwningContainer?.OnError?.Invoke(
                    ex,
                    $"Error disposing service instance of type '{instance.GetType().FullName}'"
                );
            }
        }
    }
}
