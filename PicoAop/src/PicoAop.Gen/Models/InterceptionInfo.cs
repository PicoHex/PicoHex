namespace PicoAop.Gen.Models;

/// <summary>
/// Final state of one interception chain: the surviving interceptors in
/// source order (after <c>WithoutInterceptor&lt;T&gt;()</c> exclusions) and a
/// suppression flag for <c>WithoutInterceptors()</c>.
/// </summary>
internal record InterceptionChainInfo(
    ITypeSymbol ServiceType,
    ImmutableArray<ITypeSymbol> Interceptors,
    bool Suppressed
);

internal record GlobalInterceptorInfo(ITypeSymbol InterceptorType);
