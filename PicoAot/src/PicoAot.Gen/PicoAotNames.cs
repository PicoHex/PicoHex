namespace PicoAot.Gen;

internal static class PicoAotNames
{
    public const string RootNamespace = "PicoAot";
    public const string GeneratedNamespace = "PicoAot.Generated";
    public const string WrappersClass = "PicoAotWrappers";
    public const string InterceptedPrefix = "Intercepted_";

    // Detection method names
    public const string InterceptBy = "InterceptBy";
    public const string AddInterceptor = "AddInterceptor";
    public const string WithoutInterceptor = "WithoutInterceptor";
    public const string WithoutInterceptors = "WithoutInterceptors";
}
