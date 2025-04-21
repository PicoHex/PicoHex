namespace PicoHex.Transport.HTTP;

public class HttpMiddlewarePipeline
{
    private readonly List<Func<RequestDelegate, RequestDelegate>> _middlewares = new();

    public void Use(Func<RequestDelegate, RequestDelegate> middleware) =>
        _middlewares.Add(middleware);

    public RequestDelegate Build()
    {
        RequestDelegate pipeline = ctx => Task.CompletedTask;
        foreach (var middleware in _middlewares.AsEnumerable().Reverse())
        {
            pipeline = middleware(pipeline);
        }
        return pipeline;
    }
}
