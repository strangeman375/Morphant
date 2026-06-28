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

public abstract class ByFactoryMarker<TDestination> : ConstructorMarker
{
    private ByFactoryMarker()
    {
    }
}
