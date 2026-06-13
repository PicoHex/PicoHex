namespace PicoAop.Gen.Models;

internal record InterceptionInfo(
    ITypeSymbol ServiceType,
    ITypeSymbol InterceptorType,
    ITypeSymbol? ImplType,
    bool HasMultipleRegisters
) : IEquatable<InterceptionInfo>
{
    public virtual bool Equals(InterceptionInfo? other)
    {
        if (other is null)
            return false;
        return SymbolEqualityComparer.Default.Equals(ServiceType, other.ServiceType)
            && SymbolEqualityComparer.Default.Equals(InterceptorType, other.InterceptorType);
    }

    public override int GetHashCode()
    {
        var comparer = SymbolEqualityComparer.Default;
        unchecked
        {
            var hash = 17;
            hash = (hash * 23) + (ServiceType != null ? comparer.GetHashCode(ServiceType) : 0);
            hash =
                (hash * 23) + (InterceptorType != null ? comparer.GetHashCode(InterceptorType) : 0);
            return hash;
        }
    }
}

internal record GlobalInterceptorInfo(ITypeSymbol InterceptorType, ITypeSymbol? InterfaceFilter);
