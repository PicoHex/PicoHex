namespace PicoDI.Sample.Services;

public class ConsoleLogger<T> : ILogger<T>
{
    public void Log(string message) => Console.WriteLine($"{typeof(T)} [LOG] {message}");
}
