using System.ComponentModel;

namespace PicoDI.Test;

/// <summary>
/// Tests for SvcDescriptor constructors and factory methods.
/// </summary>
public class SvcDescriptorTests
{
    // ── RED test: primary constructor serviceType param must have DynamicallyAccessedMembers annotation ──

    [Test]
    public async Task PrimaryConstructor_ServiceTypeParam_HasDynamicallyAccessedMembersInterfaces()
    {
        // The ServiceType property is annotated with [DynamicallyAccessedMembers(Interfaces)]
        // so the constructor parameter assigned to it must have the same annotation.
        // Without it, the trimmer emits IL2069 during publish.
        //
        // Note: PicoDI.Abs targets netstandard2.0 and uses a polyfill for
        // DynamicallyAccessedMembersAttribute, so we check by attribute name
        // rather than generic type lookup (the polyfill type != BCL type).

        var ctor = typeof(SvcDescriptor)
            .GetConstructors()
            .First(c =>
            {
                var ps = c.GetParameters();
                return ps.Length == 3
                    && ps[0].ParameterType == typeof(Type)
                    && ps[1].ParameterType == typeof(Type)
                    && ps[2].ParameterType == typeof(SvcLifetime);
            });
        var serviceTypeParam = ctor.GetParameters()[0];
        var attr = serviceTypeParam.CustomAttributes.FirstOrDefault(a =>
            a.AttributeType.FullName
            == "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute"
        );

        await Assert.That(attr).IsNotNull();

        // Verify it has DynamicallyAccessedMemberTypes.Interfaces (value = 0x2000)
        var memberTypesArg = attr!.ConstructorArguments.FirstOrDefault();
        await Assert.That(memberTypesArg.Value).IsNotNull();
        await Assert
            .That((int)memberTypesArg.Value!)
            .IsEqualTo((int)DynamicallyAccessedMemberTypes.Interfaces);
    }

    // ── RED test: GeneratedFactoryId should be hidden from IntelliSense ──

    [Test]
    public async Task GeneratedFactoryId_HasEditorBrowsableNever()
    {
        // Bug: GeneratedFactoryId is a source-generator-only property but lacks
        // [EditorBrowsable(Never)], unlike SingleInstance and RuntimeRegistrationId
        // which have it. This causes IDE clutter for end users.

        var property = typeof(SvcDescriptor).GetProperty(nameof(SvcDescriptor.GeneratedFactoryId))!;
        var attr = property.GetCustomAttribute<EditorBrowsableAttribute>();

        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.State).IsEqualTo(EditorBrowsableState.Never);
    }

    // ── RED test: FromInstance should reject incompatible instance types ──

    [Test]
    public async Task FromInstance_IncompatibleType_ThrowsArgumentException()
    {
        // Bug: SvcDescriptor.FromInstance does not validate that the instance
        // is assignable to the service type. A mismatched type silently registers
        // and only fails at resolution time with InvalidCastException.

        var result = () =>
            SvcDescriptor.FromInstance(
                typeof(ISimpleService),
                new ServiceWithDependency(new SimpleService()) // ← IServiceWithDependency, not ISimpleService
            );

        await Assert.That(result).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_WithFactoryDelegate_RegistersAndResolvesService()
    {
        // Arrange - use the public factory-delegate constructor
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.Register(
            new SvcDescriptor(
                typeof(ISimpleService),
                (Func<ISvcScope, object>)(_ => new SimpleService()),
                SvcLifetime.Transient
            )
        );
        container.Build();
        await using var scope = container.CreateScope();

        // Act
        var service = scope.GetService<ISimpleService>();

        // Assert
        await Assert.That(service).IsNotNull();
        await Assert.That(service).IsTypeOf<SimpleService>();
    }
}
