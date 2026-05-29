namespace PicoLog;

public sealed class FileSinkOptions
{
    public const string DefaultFilePath = "logs/app.log";

    public string FilePath
    {
        get;
        set
        {
            field = value;
            HasExplicitFilePath = true;
        }
    } = DefaultFilePath;

    public int BatchSize { get; set; } = 32;

    public int QueueCapacity { get; set; } = 4096;

    /// <summary>
    /// When <see langword="true"/> (default), an external <see cref="IFlushableLogSink.FlushAsync"/>
    /// call can interrupt the batch-filling loop, forcing an immediate flush even when
    /// the batch has not reached <see cref="BatchSize"/>. When <see langword="false"/>,
    /// batching is purely size-based and cannot be interrupted by flush requests.
    /// </summary>
    /// <remarks>
    /// This option does NOT implement periodic timed flushing. It only controls whether
    /// an explicit flush request can interrupt batch accumulation.
    /// </remarks>
    public bool AllowFlushInterrupt { get; set; } = true;

    public bool HasExplicitFilePath { get; private set; }

    public FileSinkOptions CreateValidatedCopy()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(FilePath);

        if (BatchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(BatchSize));

        if (QueueCapacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(QueueCapacity));

        return new FileSinkOptions
        {
            FilePath = FilePath,
            BatchSize = BatchSize,
            QueueCapacity = QueueCapacity,
            AllowFlushInterrupt = AllowFlushInterrupt
        };
    }
}
