using PicoHex.Protocols.CoAP;

namespace Pico.Proto.CoAP;

public class CoapOption
{
    public CoapOptionNumber Number { get; set; }
    public byte[] Value { get; set; }

    public string StringValue => Value != null ? Encoding.UTF8.GetString(Value) : "";
}
