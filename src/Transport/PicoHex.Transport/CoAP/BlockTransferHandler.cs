namespace PicoHex.Transport.CoAP;

public class BlockTransferHandler
{
    private readonly CoapOverUdpAdapter _adapter;
    private readonly Dictionary<BlockKey, List<byte[]>> _blocks = new();

    public BlockTransferHandler(CoapOverUdpAdapter adapter)
    {
        _adapter = adapter;
        _adapter.OnRequestReceived += HandleBlockTransfer;
    }

    private async Task HandleBlockTransfer(CoapOverUdpAdapter.CoapRequest request)
    {
        if (TryGetBlockOption(request.Options, out var blockOpt))
        {
            // 处理块序号并组合数据
            StoreBlock(blockOpt.Num, request.Payload);
            if (blockOpt.More)
            {
                SendBlockAck(request.MessageId, blockOpt.Num);
            }
            else
            {
                ProcessCompleteData(blockOpt.Num);
            }
        }
    }
}
