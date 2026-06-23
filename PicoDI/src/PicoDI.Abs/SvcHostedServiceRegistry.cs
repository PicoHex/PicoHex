namespace PicoDI.Abs;

/// <summary>
/// Compile-time registry of hosted service types.
/// Populated by PicoDI.Gen source generator and
/// <see cref="SvcContainerHostingExtensions.RegisterHostedSvc{THostedSvc}()"/>.
/// </summary>
public static class SvcHostedServiceRegistry
{
    public static HashSet<Type> Types { get; } = new();

    public static void Register(Type hostedType) => Types.Add(hostedType);

    public static bool Contains(Type type) => Types.Contains(type);
}
