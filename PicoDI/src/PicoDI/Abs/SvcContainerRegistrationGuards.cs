namespace PicoDI.Abs;

internal static class SvcContainerRegistrationGuards
{
    internal const string GeneratedRegistrationsNotAppliedMessage =
        "Compile-time generated registrations are required. Ensure PicoDI.Gen runs and that generated registrations are applied to this container.";

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnsureGeneratedRegistrationsApplied(ISvcContainer container)
    {
        if (!SvcContainerAutoConfiguration.HasAppliedGeneratedConfiguration(container))
        {
            throw new SourceGeneratorRequiredException(GeneratedRegistrationsNotAppliedMessage);
        }
    }
}
