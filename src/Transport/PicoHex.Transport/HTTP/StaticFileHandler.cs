namespace PicoHex.Transport.HTTP;

public class StaticFileHandler
{
    public async Task HandleRequest(HttpOverTcpAdapter.HttpContext ctx)
    {
        var filePath = Path.Combine("wwwroot", ctx.Path.TrimStart('/'));
        if (File.Exists(filePath))
        {
            ctx.Response.Body = await File.ReadAllBytesAsync(filePath);
            ctx.Response.Headers["Content-Type"] = GetMimeType(filePath);
        }
        else
        {
            ctx.Response.StatusCode = 404;
        }
    }

    private static string GetMimeType(string path) =>
        Path.GetExtension(path) switch
        {
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            _ => "application/octet-stream"
        };
}
