namespace Pico.DI.Internal;

internal static class SvcFactory
{
    internal static Func<ISvcProvider, object> CreateAotFactory(SvcDescriptor svcDescriptor)
    {
        var implementationType = svcDescriptor.ImplementationType;

        if (implementationType is null)
            throw new InvalidOperationException(
                $"The service type '{svcDescriptor.ServiceType.FullName}' has no implementation type."
            );

        var constructor =
            implementationType
                .GetConstructors()
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault()
            ?? throw new ServiceResolutionException(
                $"No public constructor found for {implementationType}."
            );

        var parameters = constructor.GetParameters();
        var dependencies = parameters.Select(p => p.ParameterType).ToList();

        DependencyGraph.AddDependency(svcDescriptor.ServiceType, dependencies);
        if (DependencyGraph.HasCycle(svcDescriptor.ServiceType, out var cyclePath))
        {
            throw new InvalidOperationException(
                $"Circular dependency detected: {string.Join(" -> ", cyclePath.Select(t => t.Name))}"
            );
        }

        var providerParam = Expression.Parameter(typeof(ISvcProvider), "sp");

        var resolveMethod =
            typeof(ISvcResolver).GetMethod(nameof(ISvcResolver.Resolve), [typeof(Type)])
            ?? throw new MissingMethodException("ISvcResolver.Resolve(Type) not found.");

        var args = parameters
            .Select(p =>
            {
                var getServiceCall = Expression.Call(
                    providerParam,
                    resolveMethod,
                    Expression.Constant(p.ParameterType)
                );
                return Expression.Convert(getServiceCall, p.ParameterType);
            })
            .ToArray<Expression>();

        var newExpr = Expression.New(constructor, args);
        var lambda = Expression.Lambda<Func<ISvcProvider, object>>(newExpr, providerParam);
        return lambda.Compile();
    }
}
