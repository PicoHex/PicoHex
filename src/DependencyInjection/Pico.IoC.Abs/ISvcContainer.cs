namespace Pico.DI.Abs;

public interface ISvcContainer
{
    ISvcContainer Register(SvcDescriptor descriptor);
    ISvcProvider GetProvider();
    List<SvcDescriptor> GetDescriptors(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    );
    SvcDescriptor GetDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type
    );
}
