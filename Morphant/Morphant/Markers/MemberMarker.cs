namespace Morphant.Markers;

public abstract class MemberMarker
{
    private protected MemberMarker()
    {
    }
}

public abstract class AutoMarker : MemberMarker
{
    private AutoMarker()
    {
    }
}

public abstract class AutoMarker<T> : MemberMarker
{
    private AutoMarker()
    {
    }
}

public abstract class IgnoreMarker : MemberMarker
{
    private IgnoreMarker()
    {
    }
}

public abstract class IgnoreMarker<T> : MemberMarker
{
    private IgnoreMarker()
    {
    }
}

public abstract class MapMarker : MemberMarker
{
    private MapMarker()
    {
    }
}

public abstract class MapMarker<T> : MemberMarker
{
    private MapMarker()
    {
    }
}
