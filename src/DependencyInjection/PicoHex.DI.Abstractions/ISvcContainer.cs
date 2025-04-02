namespace PicoHex.DI.Abstractions;

public interface ISvcContainer : IDisposable, IAsyncDisposable
{
    ISvcContainer Register(SvcDescriptor descriptor);
    ISvcProvider CreateProvider();
    List<SvcDescriptor>? GetDescriptors(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    );
    SvcDescriptor? GetDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    );
}
