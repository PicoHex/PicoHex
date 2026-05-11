namespace PicoDI.Sample.Services;

public class WelcomeService : IWelcomeService
{
    private readonly string _source;

    public WelcomeService()
    {
        _source = "default";
    }

    [SvcConstructor]
    public WelcomeService(IClock clock)
    {
        _source = $"preferred (time: {clock.Now:T})";
    }

    public string GetWelcomeMessage() => $"Resolved from: {_source}";
}
