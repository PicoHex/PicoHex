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

    /// <summary>
    /// Maximum file size in bytes before rotation. When the log file exceeds this
    /// size, it is closed, renamed with a sequence number suffix (e.g. app.log → app.1.log),
    /// and a new file is opened. Set to 0 or negative to disable. Default: disabled.
    /// </summary>
    public long MaxFileSizeBytes { get; set; }

    /// <summary>
    /// Maximum number of rotated files to retain. When exceeded, the oldest rotated
    /// file is deleted. Set to 0 or negative to keep all. Default: 0 (keep all).
    /// </summary>
    public int MaxRetainedFiles { get; set; }

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
            AllowFlushInterrupt = AllowFlushInterrupt,
            MaxFileSizeBytes = MaxFileSizeBytes,
            MaxRetainedFiles = MaxRetainedFiles,
        };
    }
}
