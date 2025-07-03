namespace Pico.DI.Internal;

internal static class DependencyGraph
{
    private static readonly ConcurrentDictionary<Type, ImmutableList<Type>> AdjacencyList = new();

    internal static void AddDependency(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
            Type serviceType,
        IEnumerable<Type> dependencies
    ) =>
        AdjacencyList.AddOrUpdate(
            serviceType,
            addValueFactory: _ => ImmutableList.CreateRange(dependencies),
            updateValueFactory: (_, existing) => existing.AddRange(dependencies)
        );

    internal static bool HasCycle(Type startType, out Stack<Type> cyclePath)
    {
        cyclePath = new Stack<Type>();
        var snapshot = AdjacencyList.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        var visited = new HashSet<Type>();
        var recursionStack = new HashSet<Type>();

        if (!snapshot.ContainsKey(startType))
            return false;
        if (!CheckCycle(startType, snapshot, visited, recursionStack, cyclePath))
            return false;
        cyclePath = new Stack<Type>(cyclePath.Reverse());
        return true;
    }

    private static bool CheckCycle(
        Type node,
        IDictionary<Type, ImmutableList<Type>> snapshot,
        HashSet<Type> visited,
        HashSet<Type> recursionStack,
        Stack<Type> path
    )
    {
        if (recursionStack.Contains(node))
        {
            path.Push(node);
            return true;
        }

        if (!visited.Add(node))
            return false;

        recursionStack.Add(node);
        path.Push(node);

        if (!snapshot.TryGetValue(node, out var neighbors))
            neighbors = ImmutableList<Type>.Empty;

        if (
            neighbors.Any(neighbor => CheckCycle(neighbor, snapshot, visited, recursionStack, path))
        )
            return true;

        recursionStack.Remove(node);
        path.Pop();
        return false;
    }
}
