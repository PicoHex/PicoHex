namespace PicoDI.Gen.OpenGeneric;

/// <summary>
/// Factory for creating open generic closure components for dependency injection and testing.
/// </summary>
internal static class OpenGenericClosureFactory
{
    private static OpenGenericClosureOrchestrator? _defaultOrchestrator;

    /// <summary>
    /// Gets the default orchestrator instance (singleton).
    /// </summary>
    public static OpenGenericClosureOrchestrator DefaultOrchestrator
    {
        get
        {
            if (_defaultOrchestrator is null)
            {
                var substitutor = new TypeParameterSubstitutor();
                var scanner = new OpenGenericScanner();
                var collector = new ClosedGenericCollector();
                var analyzer = new ClosedGenericAnalyzer();
                var generator = new ClosedGenericGenerator(substitutor);

                _defaultOrchestrator = new OpenGenericClosureOrchestrator(
                    scanner,
                    collector,
                    analyzer,
                    generator
                );
            }

            return _defaultOrchestrator;
        }
    }
}
