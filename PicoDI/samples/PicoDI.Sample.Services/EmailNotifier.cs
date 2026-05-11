namespace PicoDI.Sample.Services;

public class EmailNotifier : INotifier
{
    public string Channel => "Email";

    public void Notify(string message) => Console.WriteLine($"[Email] {message}");
}
