namespace PicoDI.Sample.Services;

public interface INotifier
{
    string Channel { get; }
    void Notify(string message);
}
