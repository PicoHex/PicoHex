namespace PicoDI.Abs;

/// <summary>
/// Represents a void (no-return-value) result for generic
/// <c>IRequest{TResponse}</c> commands and other
/// patterns that require a result type but have no meaningful value.
/// </summary>
/// <remarks>
/// This is a zero-byte value type. <c>default(VoidResult)</c> is the
/// canonical instance and is allocation-free at runtime.
/// </remarks>
public readonly struct VoidResult { }
