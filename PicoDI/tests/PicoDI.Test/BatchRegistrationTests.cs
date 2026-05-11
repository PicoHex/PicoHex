namespace PicoDI.Test;

/// <summary>
/// Tests for batch registration (RegisterRange) with all lifetimes.
/// </summary>
public class BatchRegistrationTests
{
    [Test]
    public async Task RegisterRange_MultipleDescriptors_AllRegistered()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        var descriptors = new[]
        {
            new SvcDescriptor(
                typeof(ISimpleService),
                static _ => new SimpleService(),
                SvcLifetime.Transient
            ),
            new SvcDescriptor(
                typeof(ILevelOneService),
                static _ => new LevelOneService(),
                SvcLifetime.Scoped
            ),
            new SvcDescriptor(
                typeof(IConfigurableService),
                static _ => new ConfigurableService("test"),
                SvcLifetime.Singleton
            )
        };
        container.RegisterRange(descriptors);
        await using var scope = container.CreateScope();

        // Act
        var simple = scope.GetService<ISimpleService>();
        var levelOne = scope.GetService<ILevelOneService>();
        var configurable = scope.GetService<IConfigurableService>();

        // Assert
        await Assert.That(simple).IsNotNull();
        await Assert.That(levelOne).IsNotNull();
        await Assert.That(configurable).IsNotNull();
        await Assert.That(configurable.Configuration).IsEqualTo("test");
    }

    [Test]
    public async Task RegisterRange_SameServiceType_AllAccessibleViaGetServices()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        var descriptors = new[]
        {
            new SvcDescriptor(
                typeof(INotificationService),
                static _ => new EmailNotificationService(),
                SvcLifetime.Transient
            ),
            new SvcDescriptor(
                typeof(INotificationService),
                static _ => new SmsNotificationService(),
                SvcLifetime.Transient
            ),
            new SvcDescriptor(
                typeof(INotificationService),
                static _ => new PushNotificationService(),
                SvcLifetime.Transient
            )
        };
        container.RegisterRange(descriptors);
        await using var scope = container.CreateScope();

        // Act
        var services = scope.GetServices<INotificationService>().ToList();

        // Assert
        await Assert.That(services.Count).IsEqualTo(3);
        var notificationTypes = services.Select(s => s.NotificationType).ToArray();
        await Assert.That(notificationTypes.Length).IsEqualTo(3);
        await Assert.That(notificationTypes[0]).IsEqualTo("Email");
        await Assert.That(notificationTypes[1]).IsEqualTo("SMS");
        await Assert.That(notificationTypes[2]).IsEqualTo("Push");
    }

    [Test]
    public async Task RegisterRange_MixedLifetimes_EachBehavesCorrectly()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        var descriptors = new[]
        {
            new SvcDescriptor(
                typeof(ISimpleService),
                static _ => new SimpleService(),
                SvcLifetime.Transient
            ),
            new SvcDescriptor(
                typeof(ISimpleService),
                static _ => new SimpleService(),
                SvcLifetime.Scoped
            ),
            new SvcDescriptor(
                typeof(ISimpleService),
                static _ => new SimpleService(),
                SvcLifetime.Singleton
            )
        };
        container.RegisterRange(descriptors);
        await using var scope1 = container.CreateScope();
        await using var scope2 = container.CreateScope();

        // Act
        var services1_call1 = scope1.GetServices<ISimpleService>().ToList();
        var services1_call2 = scope1.GetServices<ISimpleService>().ToList();
        var services2_call1 = scope2.GetServices<ISimpleService>().ToList();

        // Assert
        // Transient [0]: Different each call
        await Assert
            .That(services1_call1[0].InstanceId)
            .IsNotEqualTo(services1_call2[0].InstanceId);

        // Scoped [1]: Same within scope, different across scopes
        await Assert.That(services1_call1[1].InstanceId).IsEqualTo(services1_call2[1].InstanceId);
        await Assert
            .That(services1_call1[1].InstanceId)
            .IsNotEqualTo(services2_call1[1].InstanceId);

        // Singleton [2]: Same everywhere
        await Assert.That(services1_call1[2].InstanceId).IsEqualTo(services2_call1[2].InstanceId);
    }

    [Test]
    public async Task RegisterRange_EmptyCollection_NoError()
    {
        // Arrange
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Act & Assert - Should not throw
        container.RegisterRange(Array.Empty<SvcDescriptor>());
        await using var scope = container.CreateScope();

        // No assertion needed - if we get here, no exception was thrown
    }

    [Test]
    public async Task RegisterRange_WithInstance_InstanceRegistered()
    {
        // Arrange
        var preCreated = new SimpleService();
        await using var container = new SvcContainer(autoConfigureFromGenerator: false);
        var descriptors = new[] { SvcDescriptor.FromInstance(typeof(ISimpleService), preCreated) };
        container.RegisterRange(descriptors);
        await using var scope = container.CreateScope();

        // Act
        var resolved = scope.GetService<ISimpleService>();

        // Assert
        await Assert.That(resolved.InstanceId).IsEqualTo(preCreated.InstanceId);
    }
}
