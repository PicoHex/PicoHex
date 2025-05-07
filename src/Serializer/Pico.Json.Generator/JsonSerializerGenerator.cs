namespace Pico.Json.Generator;

[Generator]
public class JsonSerializerGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context) =>
        context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());

    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxReceiver is not SyntaxReceiver receiver)
            return;

        foreach (var type in receiver.Types)
        {
            var source = GenerateSerializer(type);
            context.AddSource(
                $"{type.Name}Serializer.g.cs",
                SourceText.From(source, Encoding.UTF8)
            );
        }
    }

    private string GenerateSerializer(INamedTypeSymbol type)
    {
        var code = new StringBuilder(
            $@"
using System.Text;
namespace Pico.Json.Generated;

public static class JsonSerializer<{type}>
{{
    public static string Serialize({type} value)
    {{
        var writer = new JsonWriter();
        writer.BeginObject();
        {GeneratePropertiesSerialization(type)}
        writer.EndObject();
        return writer.ToString();
    }}

    public static {type} Deserialize(string json)
    {{
        var reader = new JsonReader(json);
        reader.BeginObject();
        var obj = new {type}();
        while (reader.ReadNextProperty())
        {{
            switch (reader.CurrentProperty)
            {{
                {GeneratePropertiesDeserialization(type)}
            }}
        }}
        return obj;
    }}
}}"
        );
        return code.ToString();
    }

    // 生成属性序列化代码（示例简化）
    private string GeneratePropertiesSerialization(INamedTypeSymbol type)
    {
        var code = new StringBuilder();
        foreach (var prop in type.GetMembers().OfType<IPropertySymbol>())
            code.AppendLine($"writer.WriteProperty(\"{prop.Name}\", value.{prop.Name});");
        return code.ToString();
    }

    // 生成属性反序列化代码
    private string GeneratePropertiesDeserialization(INamedTypeSymbol type)
    {
        var code = new StringBuilder();
        foreach (var prop in type.GetMembers().OfType<IPropertySymbol>())
            code.AppendLine(
                $@"""{
                    prop.Name}"": obj.{prop.Name} = reader.Read{prop.Type.Name}(); break;"
            );
        return code.ToString();
    }
}
