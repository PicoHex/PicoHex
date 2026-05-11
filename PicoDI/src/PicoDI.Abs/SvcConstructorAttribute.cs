namespace PicoDI.Abs;

/// <summary>
/// Marks the preferred constructor for PicoDI source-generated activation.
/// Use this when a type exposes multiple public constructors and the source
/// generator should not fall back to the default widest-constructor selection.
/// </summary>
[AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
public sealed class SvcConstructorAttribute : Attribute;
