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

public interface IByFactoryMarker<out TDestination>
{
}

public abstract class ByFactoryMarker<TDestination> : ConstructorMarker, IByFactoryMarker<TDestination>
{
    private ByFactoryMarker()
    {
    }
}
