namespace PicoDI.Abs;

public static class SvcContainerGeneratedRegistrationExtensions
{
    internal const string GeneratedRegistrationsNotAppliedMessage =
        "Compile-time generated registrations are required. Ensure PicoDI.Gen runs and that generated registrations are applied to this container.";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EnsureApplied(ISvcContainer container)
    {
        if (!SvcContainerAutoConfiguration.HasAppliedGeneratedConfiguration(container))
            throw new SourceGeneratorRequiredException(GeneratedRegistrationsNotAppliedMessage);
    }

    extension(ISvcContainer container)
    {
        public ISvcContainer Register<TService, TImplementation>(SvcLifetime lifetime)
            where TImplementation : TService
        {
            EnsureApplied(container);
            return container;
        }

        public ISvcContainer Register<TService>(SvcLifetime lifetime)
            where TService : class
        {
            EnsureApplied(container);
            return container;
        }

        public ISvcContainer Register<TService>(Type implementType, SvcLifetime lifetime)
            where TService : class
        {
            EnsureApplied(container);
            return container;
        }

        public ISvcContainer RegisterTransient<TService, TImplementation>()
            where TImplementation : TService
        {
            EnsureApplied(container);
            return container;
        }

        public ISvcContainer RegisterTransient<TService>()
            where TService : class
        {
            EnsureApplied(container);
            return container;
        }

        public ISvcContainer RegisterTransient<TService>(Type implementType)
            where TService : class
        {
            EnsureApplied(container);
            return container;
        }

        public ISvcContainer RegisterScoped<TService, TImplementation>()
            where TImplementation : TService
        {
            EnsureApplied(container);
            return container;
        }

        public ISvcContainer RegisterScoped<TService>()
            where TService : class
        {
            EnsureApplied(container);
            return container;
        }

        public ISvcContainer RegisterScoped<TService>(Type implementType)
            where TService : class
        {
            EnsureApplied(container);
            return container;
        }

        public ISvcContainer RegisterSingleton<TService, TImplementation>()
            where TImplementation : TService
        {
            EnsureApplied(container);
            return container;
        }

        public ISvcContainer RegisterSingleton<TService>()
            where TService : class
        {
            EnsureApplied(container);
            return container;
        }

        public ISvcContainer RegisterSingleton<TService>(Type implementType)
            where TService : class
        {
            EnsureApplied(container);
            return container;
        }
    }
}
