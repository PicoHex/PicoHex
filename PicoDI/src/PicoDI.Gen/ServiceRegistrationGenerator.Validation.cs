namespace PicoDI.Gen;

public partial class ServiceRegistrationGenerator
{
    /// <summary>
    /// Detects circular dependencies at compile-time by analyzing the dependency graph.
    /// </summary>
    private static List<string> DetectCircularDependencies(
        IEnumerable<ServiceRegistration> registrations
    )
    {
        var cycles = new List<string>();
        var dependencyGraph = new Dictionary<string, HashSet<string>>();
        var serviceTypes = new HashSet<string>();

        foreach (var reg in registrations)
        {
            serviceTypes.Add(reg.ServiceTypeFullName);
            if (!dependencyGraph.ContainsKey(reg.ServiceTypeFullName))
                dependencyGraph[reg.ServiceTypeFullName] =  [];

            foreach (var paramTypeFullName in reg.ConstructorParameters)
            {
                dependencyGraph[reg.ServiceTypeFullName].Add(paramTypeFullName);
            }
        }

        var visited = new HashSet<string>();
        var recursionStack = new HashSet<string>();
        var path = new List<string>();

        foreach (var serviceType in serviceTypes)
        {
            DetectCycleDfs(serviceType, dependencyGraph, visited, recursionStack, path, cycles);
        }

        return cycles;
    }

    private static void DetectCycleDfs(
        string current,
        Dictionary<string, HashSet<string>> graph,
        HashSet<string> visited,
        HashSet<string> recursionStack,
        List<string> path,
        List<string> cycles
    )
    {
        if (recursionStack.Contains(current))
        {
            var cycleStart = path.IndexOf(current);
            if (cycleStart < 0)
                return;

            var cyclePath = path.Skip(cycleStart).Append(current).ToList();
            var cycleStr = string.Join(" -> ", cyclePath.Select(TypeNameDisplay.GetSimpleName));
            if (!cycles.Contains(cycleStr))
                cycles.Add(cycleStr);

            return;
        }

        if (!visited.Add(current))
            return;

        recursionStack.Add(current);
        path.Add(current);

        if (graph.TryGetValue(current, out var dependencies))
        {
            foreach (var dep in dependencies)
            {
                DetectCycleDfs(dep, graph, visited, recursionStack, path, cycles);
            }
        }

        path.RemoveAt(path.Count - 1);
        recursionStack.Remove(current);
    }
}
