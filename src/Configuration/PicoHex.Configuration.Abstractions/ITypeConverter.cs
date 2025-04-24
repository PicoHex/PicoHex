namespace PicoHex.Configuration.Abstractions;

public interface ITypeConverter<T>
{
    // 支持自定义类型转换逻辑
    T Convert(IConfigurationNode node);
}
