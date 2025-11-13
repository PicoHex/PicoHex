namespace Pico.DI.Abs;

// 标注于任意类型或程序集，通知生成器为指定闭包生成工厂
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
public sealed class PicoDiKnownGenericAttribute : Attribute
{
    public Type Service { get; }
    public Type Implementation { get; }

    public PicoDiKnownGenericAttribute(Type service, Type implementation)
    {
        Service = service;
        Implementation = implementation;
    }
}