namespace PicoDI.Test;

public class SvcHostedServiceRegistryTests
{
    [Test]
    public async Task Register_adds_type_to_Types()
    {
        var type = typeof(ISimpleService);
        SvcHostedServiceRegistry.Register(type);

        await Assert.That(SvcHostedServiceRegistry.Contains(type)).IsTrue();
    }

    [Test]
    public async Task Contains_returns_false_for_unregistered_type()
    {
        // SimpleService is NOT registered by any test — verify clean state
        await Assert.That(SvcHostedServiceRegistry.Contains(typeof(SimpleService))).IsFalse();
    }
}
