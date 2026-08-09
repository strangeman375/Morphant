namespace Morphant.Markers;

public abstract class ConstructorMarker
{
    private protected ConstructorMarker()
    {
    }
}

public sealed class ByConventionMarker : ConstructorMarker
{
    private ByConventionMarker()
    {
    }
}
