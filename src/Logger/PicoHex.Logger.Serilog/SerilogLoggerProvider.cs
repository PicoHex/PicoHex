// using SL = Serilog;
// using ILogger = PicoHex.Logger.Abstractions.ILogger;
//
// namespace PicoHex.Logger.Serilog;
//
// public class SerilogLoggerProvider(SL.ILogger logger) : ILoggerProvider
// {
//     private readonly SL.ILogger _serilogLogger = logger;
//
//     public ILogger CreateLogger(string categoryName) =>
//         new SL. SerilogLogger(_serilogLogger.ForContext("Category", categoryName));
//
//     public void Dispose() { }
// }
