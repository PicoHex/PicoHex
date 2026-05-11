namespace PicoDI.Sample.Services;

public class GreetingService(IGreeter greeter, ILogger<GreetingService> logger)
{
    public void SayHello(string name)
    {
        logger.Log($"Greeting {name}");
        Console.WriteLine(greeter.Greet(name));
    }
}
