using System.ComponentModel;
using System.Reflection;

namespace PicoDI.Test;

public class SvcDescriptorTests
{
    // ── Regression: chain-propagated [DAM] are removed, standalone remain ──

    [Test]
    public async Task PrimaryConstructor_has_no_DAM()
    {
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
        var attr = ctor.GetParameters()[0]
            .CustomAttributes.FirstOrDefault(a =>
                a.AttributeType.FullName
                == "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute"
            );
        await Assert.That(attr).IsNull();
    }

    [Test]
    public async Task ServiceType_property_has_no_DAM()
    {
        var prop = typeof(SvcDescriptor).GetProperty("ServiceType")!;
        var attr = prop.GetCustomAttributes(false)
            .FirstOrDefault(a => a.GetType().Name == "DynamicallyAccessedMembersAttribute");
        await Assert.That(attr).IsNull();
    }

    [Test]
    public async Task FromInstance_param_has_DAM()
    {
        var method = typeof(SvcDescriptor).GetMethod("FromInstance")!;
        var attr = method
            .GetParameters()[0]
            .CustomAttributes.FirstOrDefault(a =>
                a.AttributeType.FullName
                == "System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute"
            );
        await Assert.That(attr).IsNotNull();
    }

    // ── RED test: GeneratedFactoryId should be hidden from IntelliSense ──

    [Test]
    public async Task GeneratedFactoryId_HasEditorBrowsableNever()
    {
        var property = typeof(SvcDescriptor).GetProperty(nameof(SvcDescriptor.GeneratedFactoryId))!;
        var attr = property.GetCustomAttribute<EditorBrowsableAttribute>();
        await Assert.That(attr).IsNotNull();
        await Assert.That(attr!.State).IsEqualTo(EditorBrowsableState.Never);
    }

    // ── RED test: FromInstance should reject incompatible instance types ──

    [Test]
    public async Task FromInstance_IncompatibleType_ThrowsArgumentException()
    {
        var result = () =>
            SvcDescriptor.FromInstance(
                typeof(ISimpleService),
                new ServiceWithDependency(new SimpleService())
            );
        await Assert.That(result).Throws<ArgumentException>();
    }

    [Test]
    public async Task Constructor_WithFactoryDelegate_RegistersAndResolvesService()
    {
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

        var service = scope.GetService<ISimpleService>();
        await Assert.That(service).IsNotNull();
        await Assert.That(service).IsTypeOf<SimpleService>();
    }
}
