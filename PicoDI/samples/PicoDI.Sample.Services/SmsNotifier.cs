namespace PicoDI.Sample.Services;

public class SmsNotifier : INotifier
{
    public string Channel => "SMS";

    public void Notify(string message) => Console.WriteLine($"[SMS] {message}");
}
