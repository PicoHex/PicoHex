namespace PicoHex.IoC;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AutoRegisterAttribute(Type? interfaceType = null) : Attribute
{
    public Type? InterfaceType { get; } = interfaceType;
}
