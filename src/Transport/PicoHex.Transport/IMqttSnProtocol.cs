namespace PicoHex.Transport;

public interface IMqttSnProtocol : IMqttProtocol
{
    Task RegisterGatewayAsync(byte[] gatewayInfo);
    Task EnterSleepModeAsync(byte[] sleepParams);
}
