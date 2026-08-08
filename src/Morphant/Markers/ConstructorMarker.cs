namespace Morphant.Markers;

public abstract class ConstructorMarker
{
    private protected ConstructorMarker()
    {
    }
}

public abstract class ByConventionMarker : ConstructorMarker
{
    private ByConventionMarker()
    {
    }
}
