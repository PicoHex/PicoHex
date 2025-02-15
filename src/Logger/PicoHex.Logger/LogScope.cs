﻿namespace PicoHex.Logger;

public class LogScope : IDisposable
{
    private static readonly AsyncLocal<Stack<object>> Scopes = new();

    public LogScope(object state)
    {
        Scopes.Value ??= new Stack<object>();
        Scopes.Value.Push(state);
    }

    public static IReadOnlyList<KeyValuePair<string, object>>? Current
    {
        get
        {
            if (Scopes.Value is null || Scopes.Value.Count is 0)
                return null;

            return Scopes
                .Value
                .SelectMany(
                    s =>
                        s is IEnumerable<KeyValuePair<string, object>> coll
                            ? coll
                            : [new KeyValuePair<string, object>("Scope", s)]
                )
                .ToList();
        }
    }

    public void Dispose()
    {
        Scopes.Value?.Pop();
    }
}
