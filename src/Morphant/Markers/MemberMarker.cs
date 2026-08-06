using Morphant.Exceptions;

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

    public static implicit operator AutoMarker<T>(T value) =>
        throw new RuntimeInvocationNotSupportedException();
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

    public static implicit operator IgnoreMarker<T>(T value) =>
        throw new RuntimeInvocationNotSupportedException();
}

public abstract class MapMarker : MemberMarker
{
    private protected MapMarker()
    {
    }
}

public abstract class MapMarker<T> : MapMarker
{
    private MapMarker()
    {
    }

    public static implicit operator MapMarker<T>(T value) =>
        throw new RuntimeInvocationNotSupportedException();
}
