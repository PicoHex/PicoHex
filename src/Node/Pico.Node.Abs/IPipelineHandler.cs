namespace Pico.Node.Abs;

public interface IPipelineHandler
{
    /// <summary>
    /// Handles the communication for a single TCP connection.
    /// </summary>
    /// <param name="reader">The PipeReader to read incoming data from the client.</param>
    /// <param name="writer">The PipeWriter to write outgoing data to the client.</param>
    /// <param name="cancellationToken">A token to signal when the connection should be closed.</param>
    /// <returns>A task that completes when the connection handling is finished.</returns>
    Task HandleAsync(PipeReader reader, PipeWriter writer, CancellationToken cancellationToken);
}
