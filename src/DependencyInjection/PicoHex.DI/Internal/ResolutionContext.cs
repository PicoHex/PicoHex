namespace PicoHex.DI.Internal;

internal sealed class ResolutionContext
{
    private readonly ConcurrentStack<Type> _stack = new();

    public bool TryEnterResolution(Type type, out string? cyclePath)
    {
        if (_stack.Contains(type))
        {
            cyclePath = string.Join(" → ", _stack.Reverse().Append(type));
            return false;
        }
        _stack.Push(type);
        cyclePath = null;
        return true;
    }

    public void ExitResolution() => _stack.TryPop(out _);

    public bool IsEmpty => _stack.IsEmpty;
}
