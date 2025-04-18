namespace PicoHex.Protocols.CoAP;

public enum CoapResponseCode : byte
{
    Created = 0x41,
    Deleted = 0x42,
    Valid = 0x43,
    Changed = 0x44,
    Content = 0x45,
    BadRequest = 0x80,
    NotFound = 0x84,
    InternalError = 0xA0
}
