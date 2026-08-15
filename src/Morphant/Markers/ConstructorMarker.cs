namespace Morphant.Markers;

/// <summary>
/// Base type for declarative construction markers.
/// </summary>
/// <remarks>
/// Used only for compile-time binding; no runtime instance is created.
/// </remarks>
public abstract class ConstructorMarker
{
    private protected ConstructorMarker()
    {
    }
}

/// <summary>
/// Selects convention-based construction.
/// </summary>
public sealed class ByConventionMarker : ConstructorMarker
{
    private ByConventionMarker()
    {
    }
}
