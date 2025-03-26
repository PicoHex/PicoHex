namespace PicoHex.DI.Internal;

internal sealed class ResolutionContext
{
    private readonly Lock _syncRoot = new();
    private readonly Stack<Type> _dependencyChain = new();
    private readonly HashSet<Type> _activeTypes = new();

    internal bool IsEmpty
    {
        get
        {
            lock (_syncRoot)
            {
                return _dependencyChain.Count is 0;
            }
        }
    }

    internal bool TryEnterResolution(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type,
        out string? cyclePath
    )
    {
        lock (_syncRoot)
        {
            if (_activeTypes.Contains(type))
            {
                cyclePath = FormatCyclePath(type);
                return false;
            }

            _dependencyChain.Push(type);
            _activeTypes.Add(type);
            cyclePath = null;
            return true;
        }
    }

    internal void ExitResolution()
    {
        lock (_syncRoot)
        {
            if (_dependencyChain.Count is 0)
                return;

            var type = _dependencyChain.Pop();
            _activeTypes.Remove(type);
        }
    }

    private string FormatCyclePath(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type triggerType
    ) => string.Join(" → ", _dependencyChain.Reverse().Append(triggerType).Select(t => t.Name));
}
