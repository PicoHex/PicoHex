namespace PicoHex.Transport.CoAP;

public class ObserveManager
{
    private readonly CoapOverUdpAdapter _adapter;
    private readonly Dictionary<string, List<IPEndPoint>> _observers = new();

    public ObserveManager(CoapOverUdpAdapter adapter)
    {
        _adapter = adapter;
        _adapter.OnRequestReceived += CheckObserveRegistration;
    }

    private void CheckObserveRegistration(CoapOverUdpAdapter.CoapRequest request)
    {
        if (request.Options.Any(o => o.Number == OptionNumber.Observe))
        {
            RegisterObserver(request.Uri, request.Destination);
        }
    }

    public void NotifyObservers(string resourceUri, byte[] data)
    {
        foreach (var observer in _observers[resourceUri])
        {
            _adapter.SendRequestAsync(
                new CoapOverUdpAdapter.CoapRequest(
                    Code: CoapCode.GET,
                    Uri: new Uri(resourceUri),
                    Destination: observer,
                    Qos: CoapOverUdpAdapter.CoapQos.NonConfirmable
                )
            );
        }
    }
}
