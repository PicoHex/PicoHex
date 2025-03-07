var builder = new ContainerBuilder();
GeneratedIoC.ContainerRegistration.Register(builder);
var container = builder.Build();

var service = container.Resolve<IDataService>();
service.ProcessData();

public interface IDataService
{
    void ProcessData();
}

public class DataService : IDataService
{
    private readonly ILogger _logger;

    public DataService(ILogger logger)
    {
        _logger = logger;
    }

    public void ProcessData()
    {
        _logger.Log("Processing data...");
    }
}
