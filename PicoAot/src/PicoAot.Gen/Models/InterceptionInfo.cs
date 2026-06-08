using Microsoft.CodeAnalysis;

namespace PicoAot.Gen.Models;

internal record InterceptionInfo(
    ITypeSymbol ServiceType,
    ITypeSymbol InterceptorType,
    ITypeSymbol? ImplType,
    bool HasMultipleRegisters) : IEquatable<InterceptionInfo>
{
    public virtual bool Equals(InterceptionInfo? other)
    {
        if (other is null) return false;
        return SymbolEqualityComparer.Default.Equals(ServiceType, other.ServiceType)
            && SymbolEqualityComparer.Default.Equals(InterceptorType, other.InterceptorType);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = (hash * 23) + (ServiceType?.GetHashCode() ?? 0);
            hash = (hash * 23) + (InterceptorType?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

internal record GlobalInterceptorInfo(
    ITypeSymbol InterceptorType,
    ITypeSymbol? InterfaceFilter);
