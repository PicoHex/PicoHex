// 生命周期定义

using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;

namespace PicoHex.DependencyInjection.NG;

// 扩展生命周期定义
public enum Lifetime
{
    Singleton,
    Scoped,
    PerThread,
    Transient,
    Pooled // 新增池化生命周期
}

// 修改后的服务描述类
public class ServiceDescriptor
{
    public Type ServiceType { get; }
    public Type ImplementationType { get; }
    public Lifetime Lifetime { get; }
    public Func<ServiceContainer, object>? Factory { get; } // 可选的委托工厂
    public int PoolSize { get; } = 10; // 默认池大小

    public ServiceDescriptor(
        Type serviceType,
        Type implementationType,
        Lifetime lifetime,
        Func<ServiceContainer, object>? factory = null,
        int poolSize = 10
    )
    {
        ServiceType = serviceType;
        ImplementationType = implementationType;
        Lifetime = lifetime;
        Factory = factory;
        PoolSize = poolSize;
    }
}

// 对象池实现
internal class ObjectPool<T>(Func<T> createFunc, int maxSize)
{
    private readonly ConcurrentBag<T> _items = new();
    private readonly Func<T> _createFunc = createFunc;
    private int _currentCount = 0;
    private readonly int _maxSize = maxSize;

    public T Get()
    {
        if (_items.TryTake(out var item))
            return item;

        if (_currentCount < _maxSize)
        {
            Interlocked.Increment(ref _currentCount);
            return _createFunc();
        }

        throw new InvalidOperationException("Object pool exhausted");
    }

    public void Return(T item) => _items.Add(item);
}

// 注册特性（用于标记需要生成的代码）
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class RegisterServiceAttribute(Type serviceType, Lifetime lifetime) : Attribute;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class RegisterGenericAttribute(
    Type genericInterface,
    Type genericImplementation,
    Lifetime lifetime
) : Attribute;

// 服务注册记录
internal record ServiceRegistration(
    Type ImplementationType,
    Lifetime Lifetime,
    Func<Container, object> Factory
);

// 容器接口
public interface IContainer : IDisposable
{
    IServiceScope CreateScope();
    T Resolve<T>();
    object Resolve(Type serviceType);
}

// 服务作用域接口
public interface IServiceScope : IDisposable
{
    IServiceProvider ServiceProvider { get; }
    object GetService(Type serviceType);
}

// 容器核心实现
public partial class ServiceContainer : IDisposable
{
    public readonly ConcurrentDictionary<Type, ServiceDescriptor> Descriptors = new();
    private readonly ThreadLocal<Dictionary<Type, object>> _perThreadInstances = new(() => new());
    private readonly ConcurrentDictionary<Type, Lazy<object>> _singletonInstances = new();
    private readonly AsyncLocal<Dictionary<Type, object>?> _scopedInstances = new();
    public static IServiceProvider DefaultProvider => Instance.ServiceProvider;
    public IServiceProvider ServiceProvider { get; }

    // 新增池化实例存储
    private readonly ConcurrentDictionary<Type, object> _pools = new();

    public static ServiceContainer Instance { get; } = new();

    public ServiceContainer()
    {
        ServiceProvider = new ServiceProvider(this);
        Bootstrap();
    }

    public IServiceScope CreateScope() => new ServiceScope(this);

    public T GetService<T>() => (T)GetService(typeof(T));

    // 新增委托工厂注册方法
    public void Register<TService>(Func<ServiceContainer, TService> factory, Lifetime lifetime)
    {
        var descriptor = new ServiceDescriptor(
            typeof(TService),
            typeof(TService),
            lifetime,
            c => factory(c)!
        );

        Descriptors[typeof(TService)] = descriptor;
    }

    // 扩展的GetService实现
    private object GetPooledInstance(ServiceDescriptor descriptor)
    {
        var pool = _pools.GetOrAdd(
            descriptor.ImplementationType,
            _ =>
                new ObjectPool<object>(
                    () =>
                        descriptor.Factory?.Invoke(this)
                        ?? Activator.CreateInstance(descriptor.ImplementationType)!,
                    descriptor.PoolSize
                )
        );

        var pooled = ((ObjectPool<object>)pool).Get();
        return new PooledInstanceWrapper(pooled, (ObjectPool<object>)pool).Instance;
    }

    // 池化实例包装器
    private class PooledInstanceWrapper : IDisposable
    {
        public object Instance { get; }
        private readonly ObjectPool<object> _pool;

        public PooledInstanceWrapper(object instance, ObjectPool<object> pool)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        }

        public void Dispose() => _pool.Return(Instance);
    }

    // 容器验证方法
    public void Validate()
    {
        foreach (var descriptor in Descriptors.Values)
        {
            ValidateDependencies(descriptor.ImplementationType);
        }
    }

    private void ValidateDependencies(Type type)
    {
        var constructors = type.GetConstructors();
        if (constructors.Length == 0)
            return;

        var ctor = constructors.OrderByDescending(c => c.GetParameters().Length).First();
        foreach (var param in ctor.GetParameters())
        {
            if (!Descriptors.ContainsKey(param.ParameterType))
            {
                throw new InvalidOperationException(
                    $"Unregistered dependency: {param.ParameterType.Name} required by {type.Name}"
                );
            }
        }
    }

    public object? GetService(Type serviceType)
    {
        if (!Descriptors.TryGetValue(serviceType, out var descriptor))
            return null;

        try
        {
            return descriptor.Lifetime switch
            {
                Lifetime.Singleton => GetSingleton(descriptor),
                Lifetime.Scoped
                    => throw new InvalidOperationException(
                        "Resolving scoped service requires active scope"
                    ),
                Lifetime.PerThread => GetPerThread(descriptor),
                Lifetime.Transient => CreateTransient(descriptor),
                Lifetime.Pooled => GetPooledInstance(descriptor),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error resolving {serviceType}: {ex.Message}");
            return null;
        }
    }

    private object GetSingleton(ServiceDescriptor descriptor)
    {
        var lazy = _singletonInstances.GetOrAdd(
            descriptor.ImplementationType,
            _ => new Lazy<object>(() => descriptor.Factory(this), isThreadSafe: true)
        );
        return lazy.Value;
    }

    private object GetScoped(ServiceDescriptor descriptor)
    {
        if (_scopedInstances.Value == null)
            throw new InvalidOperationException("No active scope");

        if (!_scopedInstances.Value.TryGetValue(descriptor.ImplementationType, out var instance))
        {
            instance = descriptor.Factory(this);
            _scopedInstances.Value[descriptor.ImplementationType] = instance;
        }
        return instance;
    }

    private object GetPerThread(ServiceDescriptor descriptor)
    {
        var instances = _perThreadInstances.Value;
        if (!instances.TryGetValue(descriptor.ImplementationType, out var instance))
        {
            instance = descriptor.Factory(this);
            instances[descriptor.ImplementationType] = instance;
        }
        return instance;
    }

    private object CreateTransient(ServiceDescriptor descriptor) => descriptor.Factory(this);

    // Source Generator将实现这个方法
    partial void Bootstrap();

    public void Dispose()
    {
        foreach (var instance in _singletonInstances.Values.Where(x => x.IsValueCreated))
        {
            if (instance.Value is IDisposable disposable)
                disposable.Dispose();
        }
        _perThreadInstances.Dispose();
    }

    public void Register<TService, TImplementation>(Lifetime lifetime)
        where TImplementation : TService
    {
        var descriptor = new ServiceDescriptor(
            typeof(TService),
            typeof(TImplementation),
            lifetime,
            CreateFactory<TImplementation>()
        );
        Descriptors[typeof(TService)] = descriptor;
    }

    private Func<ServiceContainer, object> CreateFactory<T>()
    {
        // 实际工厂生成逻辑需要由Source Generator实现
        // 这里使用占位符代码说明原理
        return c =>
        {
            // 生成类似这样的代码：
            // new TImpl(c.Resolve<IDep1>(), c.Resolve<IDep2>())
            return Activator.CreateInstance<T>()!;
        };
    }
}

// 作用域实现
public class ServiceScope : IServiceScope
{
    private readonly ConcurrentDictionary<Type, object> _scopedInstances = new();
    private readonly object _syncRoot = new();

    public IServiceProvider ServiceProvider { get; }
    internal ServiceContainer Container { get; }

    public ServiceScope(ServiceContainer container)
    {
        Container = container;
        ServiceProvider = new ServiceProvider(this);
    }

    public object GetService(Type serviceType)
    {
        if (!Container.Descriptors.TryGetValue(serviceType, out var descriptor))
            return null;

        if (descriptor.Lifetime != Lifetime.Scoped)
            return Container.GetService(serviceType);

        if (_scopedInstances.TryGetValue(serviceType, out var instance))
            return instance;

        lock (_syncRoot)
        {
            if (!_scopedInstances.TryGetValue(serviceType, out instance))
            {
                instance = descriptor.Factory(Container);
                _scopedInstances[serviceType] = instance;
            }
            return instance;
        }
    }

    public void Dispose()
    {
        foreach (var instance in _scopedInstances.Values.OfType<IDisposable>())
            instance.Dispose();
        _scopedInstances.Clear();
    }
}

// Source Generator生成的代码示例
[Generator]
public class ContainerGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new ServiceRegistrationReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var receiver = (ServiceRegistrationReceiver)context.SyntaxReceiver;
        var attributeArguments = new Dictionary<AttributeSyntax, List<string>>();

        foreach (var syntaxTree in receiver.RegisteredClasses.Select(c => c.SyntaxTree).Distinct())
        {
            var semanticModel = context.Compilation.GetSemanticModel(syntaxTree);
            var classesInTree = receiver.RegisteredClasses.Where(c => c.SyntaxTree == syntaxTree);

            foreach (var classDecl in classesInTree)
            {
                foreach (var attribute in classDecl.AttributeLists.SelectMany(al => al.Attributes))
                {
                    var args =
                        attribute.ArgumentList?.Arguments.Select(a => a.Expression)
                        ?? Enumerable.Empty<ExpressionSyntax>();

                    var parsedArgs = args.Select(expr => GetAttributeArgument(expr, semanticModel))
                        .ToList();

                    attributeArguments[attribute] = parsedArgs;
                }
            }
        }

        // 生成代码时使用参数列表
        var codeBuilder = new StringBuilder();
        foreach (var (attribute, args) in attributeArguments)
        {
            var attributeName = attribute.Name.ToString();

            switch (attributeName)
            {
                case "RegisterService":
                    GenerateServiceRegistration(codeBuilder, args);
                    break;
                case "RegisterGeneric":
                    GenerateGenericRegistration(codeBuilder, args);
                    break;
                case "RegisterPooled":
                    GeneratePooledRegistration(codeBuilder, args);
                    break;
            }
        }

        context.AddSource("ServiceContainer.Generated.cs", codeBuilder.ToString());
    }

    private void GenerateServiceRegistration(StringBuilder code, List<string> args)
    {
        // args[0] = 服务类型
        // args[1] = 生命周期
        var serviceType = args[0];
        var lifetime = args[1];

        code.AppendLine(
            $@"
        Register({serviceType}, {lifetime}, 
            c => new {GetImplementationType(serviceType)}(/* 构造函数参数 */));"
        );
    }

    private void GeneratePooledRegistration(StringBuilder code, List<string> args)
    {
        // args[0] = 服务类型
        // args[1] = 池大小（可选）
        var serviceType = args[0];
        var poolSize = args.Count > 1 ? args[1] : "10";

        code.AppendLine(
            $@"
        RegisterPooled({serviceType}, 
            poolSize: {poolSize},
            factory: c => new {GetImplementationType(serviceType)}(/* 参数 */));"
        );
    }

    private void GenerateGenericRegistration(StringBuilder code, List<string> args)
    {
        if (args.Count < 3)
        {
            throw new InvalidOperationException(
                "RegisterGeneric attribute requires at least 3 arguments: "
                    + "genericInterface, genericImplementation, lifetime"
            );
        }

        var genericInterface = ParseGenericType(args[0]);
        var genericImplementation = ParseGenericType(args[1]);
        var lifetime = args[2];

        // 验证泛型类型参数
        if (!genericInterface.IsOpenGeneric)
        {
            throw new InvalidOperationException(
                $"Generic interface {genericInterface} must be an open generic type"
            );
        }

        if (!genericImplementation.IsOpenGeneric)
        {
            throw new InvalidOperationException(
                $"Generic implementation {genericImplementation} must be an open generic type"
            );
        }

        // 生成类型约束代码
        var constraints = GenerateTypeConstraints(genericInterface, genericImplementation);

        code.AppendLine(
            $@"
        RegisterGeneric(
            serviceType: {ToTypeOfString(genericInterface)},
            implementationType: {ToTypeOfString(genericImplementation)},
            lifetime: {lifetime},
            factory: (container, typeArgs) => {GenerateFactoryMethod(genericImplementation, constraints)});"
        );
    }

    private (string TypeName, int Arity, bool IsOpenGeneric) ParseGenericType(string typeExpression)
    {
        var match = Regex.Match(typeExpression, @"^typeof\((.+)\)$");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Invalid type expression: {typeExpression}");
        }

        var typeName = match.Groups[1].Value;
        var arity = typeName.Count(c => c == ',') + 1;
        var isOpenGeneric = typeName.Contains("<>");

        return (typeName, arity, isOpenGeneric);
    }

    private string GenerateTypeConstraints(
        (string TypeName, int Arity, bool IsOpenGeneric) interfaceType,
        (string TypeName, int Arity, bool IsOpenGeneric) implementationType
    )
    {
        var sb = new StringBuilder();
        var interfaceArgs = string.Join(
            ", ",
            Enumerable.Range(0, interfaceType.Arity).Select(i => "T" + (i + 1))
        );
        var implArgs = string.Join(
            ", ",
            Enumerable.Range(0, implementationType.Arity).Select(i => "T" + (i + 1))
        );

        sb.AppendLine(
            $"where TInterface : {interfaceType.TypeName.Replace("<>", $"<{interfaceArgs}>")}"
        );
        sb.AppendLine(
            $"where TImplementation : {implementationType.TypeName.Replace("<>", $"<{implArgs}>")}"
        );
        return sb.ToString();
    }

    private string GenerateFactoryMethod(
        (string TypeName, int Arity, bool IsOpenGeneric) implementationType,
        string constraints
    )
    {
        return $@"
        ActivatorUtilities.CreateFactory<{implementationType .TypeName .Replace("<>", "<TImplementation>")}>(
            constructorSignature: new[] {{ typeof(IServiceProvider) }},
            parameterTypes: new Type[0])";
    }

    // 扩展方法帮助生成类型表达式
    private string ToTypeOfString((string TypeName, int Arity, bool _) type)
    {
        var genericArgs = string.Join(
            ", ",
            Enumerable.Range(0, type.Arity).Select(i => "T" + (i + 1))
        );
        return $"typeof({type.TypeName.Replace("<>", $"<{genericArgs}>")})";
    }

    // 辅助方法获取实现类型名称
    private string GetImplementationType(string serviceType)
    {
        // 这里实现类型推导逻辑
        return serviceType.Replace("I", "") + "Impl"; // 示例简化
    }

    private string GetAttributeArgument(SyntaxNode argumentNode, SemanticModel semanticModel)
    {
        return argumentNode switch
        {
            // 处理 typeof 表达式
            TypeOfExpressionSyntax typeOfExpr => HandleTypeOfExpression(typeOfExpr, semanticModel),

            // 处理成员访问表达式（如枚举值）
            MemberAccessExpressionSyntax memberAccess
                => HandleMemberAccess(memberAccess, semanticModel),

            // 处理字面量表达式
            LiteralExpressionSyntax literal => HandleLiteral(literal),

            // 处理泛型类型名称
            GenericNameSyntax genericName => genericName.ToString(),

            // 处理带括号的表达式
            ParenthesizedExpressionSyntax parenthesized
                => GetAttributeArgument(parenthesized.Expression, semanticModel),

            // 处理标识符名称
            IdentifierNameSyntax identifier => identifier.ToString(),

            // 处理默认值表达式
            DefaultExpressionSyntax defaultExpr
                => HandleDefaultExpression(defaultExpr, semanticModel),

            // 处理其他未识别的情况
            _
                => throw new NotSupportedException(
                    $"Unsupported argument type: {argumentNode.GetType().Name}"
                )
        };
    }

    private string HandleTypeOfExpression(
        TypeOfExpressionSyntax typeOfExpr,
        SemanticModel semanticModel
    )
    {
        var typeInfo = ModelExtensions.GetTypeInfo(semanticModel, typeOfExpr.Type);
        if (typeInfo.Type is INamedTypeSymbol typeSymbol)
        {
            var typeName = GetFullTypeName(typeSymbol);
            return $"typeof({typeName})";
        }
        return $"typeof({typeOfExpr.Type})";
    }

    private string HandleMemberAccess(
        MemberAccessExpressionSyntax memberAccess,
        SemanticModel semanticModel
    )
    {
        var symbol = ModelExtensions.GetSymbolInfo(semanticModel, memberAccess).Symbol;
        if (
            symbol is IFieldSymbol fieldSymbol
            && fieldSymbol.ContainingType.TypeKind == TypeKind.Enum
        )
        {
            return $"{GetFullTypeName(fieldSymbol.ContainingType)}.{fieldSymbol.Name}";
        }
        return memberAccess.ToString();
    }

    private string HandleLiteral(LiteralExpressionSyntax literal)
    {
        return literal.Kind() switch
        {
            SyntaxKind.StringLiteralExpression => $"\"{literal.Token.ValueText}\"",
            SyntaxKind.CharacterLiteralExpression => $"'{literal.Token.ValueText}'",
            SyntaxKind.NumericLiteralExpression => literal.Token.ValueText,
            SyntaxKind.TrueLiteralExpression => "true",
            SyntaxKind.FalseLiteralExpression => "false",
            _ => literal.ToString()
        };
    }

    private string HandleDefaultExpression(
        DefaultExpressionSyntax defaultExpr,
        SemanticModel semanticModel
    )
    {
        var typeInfo = ModelExtensions.GetTypeInfo(semanticModel, defaultExpr.Type);
        if (typeInfo.Type != null)
        {
            return $"default({GetFullTypeName(typeInfo.Type)})";
        }
        return $"default({defaultExpr.Type})";
    }

    private static string GetFullTypeName(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol namedType)
        {
            var typeArguments = namedType.IsGenericType
                ? $"<{string.Join(", ", namedType.TypeArguments.Select(GetFullTypeName))}>"
                : "";

            return $"{namedType.ContainingNamespace.ToDisplayString()}.{namedType.Name}{typeArguments}";
        }
        return typeSymbol.ToDisplayString();
    }
}

internal class ServiceRegistrationReceiver : ISyntaxReceiver
{
    public List<ClassDeclarationSyntax> RegisteredClasses { get; } = new();

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {
        if (
            syntaxNode is ClassDeclarationSyntax classDecl
            && classDecl
                .AttributeLists
                .Any(
                    al =>
                        al.Attributes.Any(
                            a => a.Name.ToString() is "RegisterService" or "RegisterGeneric"
                        )
                )
        )
        {
            RegisteredClasses.Add(classDecl);
        }
    }
}

// 服务提供者接口实现
public class ServiceProvider : IServiceProvider
{
    private readonly ServiceContainer _container;
    private readonly IServiceScope? _scope;

    public ServiceProvider(ServiceContainer container)
    {
        _container = container;
    }

    public ServiceProvider(IServiceScope scope)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _container = ((ServiceScope)scope).Container;
    }

    public object? GetService(Type serviceType)
    {
        try
        {
            return _scope != null
                ? _scope.GetService(serviceType)
                : _container.GetService(serviceType);
        }
        catch (InvalidOperationException)
        {
            return null; // 符合IServiceProvider规范
        }
    }
}
