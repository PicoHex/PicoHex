namespace PicoMediator.Tests;

public class PublishFanoutTests
{
    public record OrderCreated(Guid OrderId, string Item) : INotification;

    public sealed class OrderCreatedEmailHandler : INotificationHandler<OrderCreated>
    {
        public List<OrderCreated> Received { get; } = [];

        public ValueTask Handle(OrderCreated n, CancellationToken ct)
        {
            Received.Add(n);
            return ValueTask.CompletedTask;
        }
    }

    public sealed class OrderCreatedAuditHandler : INotificationHandler<OrderCreated>
    {
        public List<OrderCreated> Received { get; } = [];

        public ValueTask Handle(OrderCreated n, CancellationToken ct)
        {
            Received.Add(n);
            return ValueTask.CompletedTask;
        }
    }

    [Test]
    public async Task Publish_FansOutToAllHandlers()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        var emailHandler = new OrderCreatedEmailHandler();
        var auditHandler = new OrderCreatedAuditHandler();
        container.RegisterSingle<INotificationHandler<OrderCreated>>(emailHandler);
        container.RegisterSingle<INotificationHandler<OrderCreated>>(auditHandler);
        container.Build();
        await using var scope = container.CreateScope();

        var mediator = new Mediator(scope);
        var notification = new OrderCreated(Guid.NewGuid(), "book");
        await mediator.Publish(notification);

        await Assert.That(emailHandler.Received.Count).IsEqualTo(1);
        await Assert.That(auditHandler.Received.Count).IsEqualTo(1);
        await Assert.That(emailHandler.Received[0].Item).IsEqualTo("book");
    }

    [Test]
    public async Task Publish_NoHandlers_CompletesSilently()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        container.Build();
        await using var scope = container.CreateScope();
        var mediator = new Mediator(scope);

        await mediator.Publish(new OrderCreated(Guid.NewGuid(), "x"));
    }
}
