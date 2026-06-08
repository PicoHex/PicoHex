namespace PicoDI.Test;

/// <summary>
/// Tests for SvcDescriptor constructors and factory methods.
/// </summary>
public class SvcDescriptorTests
{
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
