namespace PicoAop.Abs;

public interface IInvocation<TResult>
{
    string MethodName { get; }
    Type ServiceType { get; }
    TResult Result { get; set; }
}
