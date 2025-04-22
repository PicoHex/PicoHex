namespace PicoHex.DI.Abstractions;

public interface ISvcContainer
{
    ISvcContainer Register(SvcDescriptor descriptor);
    ISvcProvider GetProvider();
    List<SvcDescriptor> GetDescriptors(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.None)] Type type
    );
    SvcDescriptor GetDescriptor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.None)] Type type
    );
}
