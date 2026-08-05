namespace PicoMediator.Tests;

// Regression for bridge defect #2 (2026-08-04): the generated handlers
// configurator id must ALWAYS Ordinal-sort BEFORE the generated bridges
// configurator id, regardless of the assembly name. Otherwise the bridge
// registration occupies ISubscriber<Base> first and the handlers
// configurator's IsRegistered dedup silently skips user base-key
// subscribers ("base-key direct subscribers + concrete subscribers" contract).
//
// The id templates below MUST mirror MediatorGenerator.cs (fixed state):
//   handlers: $"pico-mediator::0::{assemblyName}::{className}"   (line ~153)
//   bridges:  $"pico-mediator::1::{assemblyName}::{className}"   (line ~406)
// '0' < '1' in Ordinal order — handlers always apply before bridges,
// independent of the assembly name.
public class ConfiguratorOrderingTests
{
    // Deliberately has NO implementers in this assembly: the real generated
    // configurators never touch this key, isolating the test from them.
    public interface IFakeBase : IEvent { }

    public sealed class FakeBridge : ISubscriber<IFakeBase>
    {
        public ValueTask Handle(IFakeBase e, CancellationToken ct) => ValueTask.CompletedTask;
    }

    public sealed class FakeUnifiedSubscriber : ISubscriber<IFakeBase>
    {
        public ValueTask Handle(IFakeBase e, CancellationToken ct) => ValueTask.CompletedTask;
    }

    [Test]
    public async Task HandlersConfiguratorId_AlwaysSortsBeforeBridgesConfiguratorId()
    {
        // Battery of assembly names: lowercase (the defect trigger with the old
        // "events::" template), uppercase, digit, underscore.
        var assemblyNames = new[] { "myapp", "PicoMediator.Tests", "Zapp", "123app", "_app" };

        foreach (var asm in assemblyNames)
        {
            var handlersId = $"pico-mediator::0::{asm}::MediatorHandlerRegistrations_X";
            var bridgesId = $"pico-mediator::1::{asm}::MediatorEventDispatchers_X";

            await Assert.That(string.CompareOrdinal(handlersId, bridgesId)).IsLessThan(0);
        }
    }

    [Test]
    public async Task UserBaseKeySubscriber_IsNotSkipped_WhenBridgeConfiguratorSortsFirst()
    {
        // Single assembly name ("myapp" — the defect trigger): the registry is
        // global, so a multi-name loop would accumulate fake configurators and
        // pollute every container. Isolation comes from the IFakeBase key.
        // NOTE: the two fake configurators registered below PERSIST in the
        // global registry for the rest of this test process and are applied to
        // every subsequent container (AddPicoMediator) — harmless because they
        // only ever register the IFakeBase key, which no other test touches.
        var asm = "myapp";

        var container = new SvcContainer(autoConfigureFromGenerator: false);

        // Simulated generated configurators with the generator's FIXED
        // id templates ('0' handlers < '1' bridges, Ordinal).
        MediatorAutoSubscriptionRegistry.Register(
            $"pico-mediator::1::{asm}::MediatorEventDispatchers_X", // bridges: no dedup (infrastructure)
            c =>
                c.Register(
                    SvcDescriptor.Create(
                        typeof(ISubscriber<IFakeBase>),
                        static _ => new FakeBridge(),
                        SvcLifetime.Transient
                    )
                )
        );
        MediatorAutoSubscriptionRegistry.Register(
            $"pico-mediator::0::{asm}::MediatorHandlerRegistrations_X", // handlers: IsRegistered dedup
            c =>
            {
                if (!c.IsRegistered(typeof(ISubscriber<IFakeBase>)))
                    c.Register(
                        SvcDescriptor.Create(
                            typeof(ISubscriber<IFakeBase>),
                            static _ => new FakeUnifiedSubscriber(),
                            SvcLifetime.Transient
                        )
                    );
            }
        );

        container.AddPicoMediator();
        container.Build();
        await using var scope = container.CreateScope();

        var subscribers = scope.GetServices<ISubscriber<IFakeBase>>();

        // Contract: bridge + user base-key subscriber must coexist (2).
        await Assert.That(subscribers.Count).IsEqualTo(2);
    }
}
