# Pico.DI Source Generator

## Overview

Pico.DI Source Generator is a compile-time code generation tool that enhances the Pico.DI dependency injection container with AOT (Ahead-of-Time) compilation support. It analyzes your project at compile time to generate optimized service registration and resolution code.

## Features

### 1. Compile-Time Service Discovery
- Automatically discovers service interfaces and implementations
- Generates service registration code at compile time
- Eliminates runtime reflection for known services

### 2. AOT-Friendly Factory Generation
- Generates factory methods for service instantiation
- Avoids Expression.Compile() calls in AOT environments
- Provides fallback mechanisms for runtime services

### 3. Circular Dependency Detection
- Analyzes dependency graphs at compile time
- Reports circular dependencies as build errors
- Prevents runtime circular dependency exceptions

### 4. Performance Optimizations
- Compile-time service resolution
- Reduced runtime overhead
- Better startup performance

## Usage

### Basic Usage

```csharp
// Create AOT-optimized container
var container = AotBootstrap.CreateAotContainer();

// Register services (these will be analyzed by the source generator)
container
    .RegisterTransient<IUserService, UserService>()
    .RegisterTransient<IEmailService, EmailService>();

var provider = container.GetProvider();
var userService = provider.Resolve<IUserService>();
```

### Environment-Specific Optimization

```csharp
// Automatically chooses the best container for the environment
var container = AotBootstrap.CreateOptimizedContainer();
```

### Compile-Time Only Registration

```csharp
// Uses only compile-time discovered services
var container = AotBootstrap.CreateCompileTimeContainer();
```

## How It Works

### 1. Compile-Time Analysis

The source generator analyzes your project during compilation:
- Scans for interface declarations (starting with 'I')
- Identifies implementation classes
- Analyzes constructor dependencies
- Builds dependency graphs

### 2. Code Generation

Generates three main components:

#### Service Registrations
```csharp
// Generated in Pico.DI.ServiceRegistration.g.cs
public static class GeneratedServiceRegistrations
{
    public static void Register(ISvcContainer container)
    {
        container.RegisterTransient<IUserService, UserService>();
        container.RegisterTransient<IEmailService, EmailService>();
        // ... more registrations
    }
}
```

#### Factory Methods
```csharp
// Generated in Pico.DI.FactoryMethods.g.cs
public static class GeneratedFactoryMethods
{
    public static object CreateUserService(ISvcProvider provider)
    {
        return new UserService();
    }
    
    public static object CreateNotificationService(ISvcProvider provider)
    {
        var userService = (IUserService)provider.Resolve(typeof(IUserService));
        var emailService = (IEmailService)provider.Resolve(typeof(IEmailService));
        return new NotificationService(userService, emailService);
    }
}
```

#### AOT Container Implementation
```csharp
// Generated in Pico.DI.AotContainer.g.cs
public sealed class AotServiceContainer : ISvcContainer
{
    // AOT-optimized implementation
}
```

### 3. Runtime Integration

- The generated code integrates seamlessly with existing Pico.DI interfaces
- Provides fallback to runtime resolution for dynamically registered services
- Maintains compatibility with existing code

## Benefits

### AOT Compatibility
- No runtime reflection for compile-time known services
- Expression.Compile() calls are avoided
- Suitable for iOS, Android, and other AOT environments

### Performance
- Faster service resolution
- Reduced memory allocations
- Better startup times

### Developer Experience
- Compile-time error detection
- Better IntelliSense support
- No attribute annotations required

## Configuration

### Project Setup

Add the source generator reference to your project:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <ItemGroup>
        <ProjectReference Include="..\Pico.DI.SourceGen\Pico.DI.SourceGen.csproj" 
                         OutputItemType="Analyzer" 
                         ReferenceOutputAssembly="false" />
    </ItemGroup>
</Project>
```

### Build Configuration

No additional configuration required. The source generator automatically activates when the project is built.

## Limitations

### Dynamic Services
- Services registered at runtime (not known at compile time) use fallback resolution
- Dynamic service registration still works but without AOT optimizations

### Complex Scenarios
- Very complex dependency graphs may have limited compile-time analysis
- External assemblies are not analyzed by the source generator

## Troubleshooting

### Common Issues

1. **Circular Dependencies**: Reported as build errors with detailed dependency paths
2. **Missing Services**: Ensure interfaces and implementations are in the same compilation
3. **Performance**: Check that services are properly discovered and registered

### Diagnostics

The source generator provides detailed diagnostics:
- PICO001: Circular dependency detected
- PICO002: Source generation failed
- PICO003: Service registration generation failed

## Migration from Regular Pico.DI

### Step 1: Update Project References
Add the source generator reference to your project.

### Step 2: Update Container Creation
Replace:
```csharp
var container = Bootstrap.CreateContainer();
```

With:
```csharp
var container = AotBootstrap.CreateAotContainer();
```

### Step 3: Test and Optimize
- Run your application to ensure compatibility
- Monitor performance improvements
- Adjust service registrations as needed

## Future Enhancements

- Support for external assembly analysis
- Advanced dependency graph optimizations
- Integration with build pipelines
- Enhanced diagnostics and reporting