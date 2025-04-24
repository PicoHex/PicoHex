namespace PicoHex.Configuration.Abstractions;

public interface IConfigurationNode
{
    // 强类型值访问（支持泛型）
    T GetValue<T>(string path = null);

    // 获取子树（保留结构化信息）
    IConfigurationNode GetSection(string path);

    // 转换为动态对象（如 DynamicJson）
    dynamic AsDynamic();

    // 绑定到 POCO 对象（替代 IOptions<T>）
    T Bind<T>()
        where T : new();
}
