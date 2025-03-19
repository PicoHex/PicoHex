namespace PicoHex.IoC.Abstractions;

public interface ISvcContainer
{
    ISvcContainer Register(SvcDescriptor descriptor);
}
