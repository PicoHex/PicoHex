namespace PicoHex.DependencyInjection.Abstractions;

public interface ISvcScope : IResolver, IDisposable, IAsyncDisposable;
