namespace PicoHex.Server.Http
{
    public class HandlerFactory(
        ILogger<HttpHandler> httpLogger,
        ILogger<RestfulHandler> restfulLogger
    ) : IHandlerFactory
    {
        private readonly ILogger<HttpHandler> _httpLogger =
            httpLogger ?? throw new ArgumentNullException(nameof(httpLogger));
        private readonly ILogger<RestfulHandler> _restfulLogger =
            restfulLogger ?? throw new ArgumentNullException(nameof(restfulLogger));

        public IStreamHandler GetHandler(string path)
        {
            if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                // Use RestfulHandler for paths starting with "/api"
                return new RestfulHandler(_restfulLogger);
            }

            // Default to HttpHandler
            return new HttpHandler(_httpLogger);
        }
    }
}
