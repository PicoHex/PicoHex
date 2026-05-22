namespace PicoDI.Abs;

public interface IInvocation<TResult>
{
    string MethodName { get; }
    Type ServiceType { get; }
    ISvcScope? Scope { get; }
    TResult Result { get; set; }
}
