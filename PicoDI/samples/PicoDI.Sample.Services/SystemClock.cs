namespace PicoDI.Sample.Services;

public class SystemClock : IClock
{
    public DateTime Now => DateTime.UtcNow;
}
