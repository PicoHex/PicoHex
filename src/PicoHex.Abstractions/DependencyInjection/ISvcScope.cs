namespace PicoHex.Abstractions.DependencyInjection;

public interface ISvcScope : IResolver, IDisposable, IAsyncDisposable;
