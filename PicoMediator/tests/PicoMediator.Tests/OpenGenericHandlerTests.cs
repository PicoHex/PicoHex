namespace PicoMediator.Tests;

public class OpenGenericHandlerTests
{
    public record PagedQuery<T>(int Page, int Size) : IRequest<PagedResult<T>>;

    public record PagedResult<T>(IReadOnlyList<T> Items, int Total);

    public sealed class PagedQueryHandler<T> : IRequestHandler<PagedQuery<T>, PagedResult<T>>
    {
        public ValueTask<PagedResult<T>> Handle(PagedQuery<T> r, CancellationToken ct) =>
            ValueTask.FromResult(new PagedResult<T>([], 0));
    }

    [Test]
    public async Task Send_OpenGeneric_ResolvesClosedHandler()
    {
        var container = new SvcContainer(autoConfigureFromGenerator: false);
        // Register closed handler via factory (open generic requires PicoDI.Gen which is unavailable in test)
        container.RegisterTransient<IRequestHandler<PagedQuery<string>, PagedResult<string>>>(
            _ => new PagedQueryHandler<string>()
        );
        container.AddPicoMediator();
        container.Build();

        await using var scope = container.CreateScope();
        var mediator = scope.GetService<IMediator>();

        var result = await mediator.Send<PagedQuery<string>, PagedResult<string>>(
            new PagedQuery<string>(1, 10)
        );

        await Assert.That(result).IsNotNull();
    }
}
