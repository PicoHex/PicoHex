namespace PicoHex.DI.Internal;

internal sealed class ResolutionContext
{
    private readonly ConcurrentStack<Type> _dependencyChain = new();
    private readonly ConcurrentDictionary<Type, byte> _activeTypes = new();

    internal bool TryEnterResolution(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type,
        out string? cyclePath
    )
    {
        if (!_activeTypes.TryAdd(type, 0))
        {
            cyclePath = FormatCyclePath(type);
            return false;
        }
        _dependencyChain.Push(type);
        cyclePath = null;
        return true;
    }

    internal void ExitResolution()
    {
        if (_dependencyChain.TryPop(out var type))
            _activeTypes.TryRemove(type, out _);
    }

    private string FormatCyclePath(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type triggerType
    ) => string.Join(" → ", _dependencyChain.Reverse().Append(triggerType).Select(t => t.Name));
}
