namespace PicoHex.DI;

internal sealed class ResolutionContext
{
    private readonly Lock _syncRoot = new();
    private readonly Stack<Type> _dependencyChain = new();
    private readonly HashSet<Type> _activeTypes = new();

    public bool IsEmpty
    {
        get
        {
            lock (_syncRoot)
            {
                return _dependencyChain.Count == 0;
            }
        }
    }

    public bool TryEnterResolution(
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

    public void ExitResolution()
    {
        lock (_syncRoot)
        {
            if (_dependencyChain.Count == 0)
                return;

            var type = _dependencyChain.Pop();
            _activeTypes.Remove(type);
        }
    }

    private string FormatCyclePath(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type triggerType
    )
    {
        var path = _dependencyChain.Reverse().Append(triggerType).Select(t => t.Name);
        return string.Join(" → ", path);
    }
}
