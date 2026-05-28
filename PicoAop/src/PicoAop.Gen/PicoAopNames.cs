namespace PicoAop.Gen;

internal static class PicoAopNames
{
    // Method names (for syntax matching)
    public const string InterceptBy = "InterceptBy";
    public const string AddInterceptor = "AddInterceptor";
    public const string WithoutInterceptor = "WithoutInterceptor";
    public const string WithoutInterceptors = "WithoutInterceptors";
    public const string Register = "Register";
    public const string RegisterTransient = "RegisterTransient";
    public const string RegisterScoped = "RegisterScoped";
    public const string RegisterSingleton = "RegisterSingleton";

    // PicoAop type names (for semantic matching)
    public const string IInterceptorFull = "PicoAop.Abs.IInterceptor";
    public const string InterceptorBaseFull = "PicoAop.Abs.InterceptorBase";
    public const string VoidResultFull = "PicoAop.Abs.VoidResult";
    public const string IInvocationFull = "PicoAop.Abs.IInvocation`1";

    // PicoDI type names (for semantic matching — cross-package references)
    public const string ISvcContainerFull = "PicoDI.Abs.ISvcContainer";
    public const string ISvcScopeFull = "PicoDI.Abs.ISvcScope";
    public const string SvcContainerFull = "PicoDI.SvcContainer";
    public const string SvcContainerAutoConfigFull = "PicoDI.SvcContainerAutoConfiguration";

    // Generated names
    public const string GeneratedNamespace = "PicoAop.Generated";
    public const string ConfiguratorId = "intercepted::PicoAop";
}
