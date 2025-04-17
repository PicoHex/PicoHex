namespace PicoHex.Protocols.MQTT;

public static class MqttPacketIdGenerator
{
    private static int _packetIdCounter = 0;

    /// <summary>
    /// 生成唯一的 Packet Identifier（线程安全，循环递增）
    /// </summary>
    public static ushort GeneratePacketId()
    {
        // 原子递增计数器，并处理溢出（确保值在 1~65535 之间循环）
        int newId = Interlocked.Increment(ref _packetIdCounter);
        return (ushort)((newId - 1) % ushort.MaxValue + 1);
    }
}