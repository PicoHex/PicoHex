using PicoDI.Abs;
using PicoMediator.Abs;

namespace PicoMediator;

/// <summary>
/// Request dispatch. When PicoMediator.Gen is referenced, the generated
/// <c>GeneratedDispatch.Switch.g.cs</c> replaces this class with a
/// compiled switch for zero-allocation dispatch.
/// Without the generator, falls back to <see cref="ISvcScope.GetService"/>.
/// </summary>
internal static class GeneratedDispatch
{
    internal static ValueTask<TResponse> Send<TRequest, TResponse>(
        ISvcScope scope,
        TRequest request,
        CancellationToken ct
    )
        where TRequest : IRequest<TResponse>
    {
        var handler = scope.GetService<IRequestHandler<TRequest, TResponse>>();
        if (handler is null)
            throw new InvalidOperationException(
                $"No handler registered for {typeof(TRequest).FullName}."
            );

        return handler.Handle(request, ct);
    }
}
