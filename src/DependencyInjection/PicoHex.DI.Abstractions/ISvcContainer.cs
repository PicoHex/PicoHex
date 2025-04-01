namespace PicoHex.DI.Abstractions;

public interface ISvcContainer : IDisposable, IAsyncDisposable
{
    ISvcContainer Register(SvcDescriptor descriptor);
    ISvcProvider CreateProvider();
    SvcDescriptor? GetDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    );
}
