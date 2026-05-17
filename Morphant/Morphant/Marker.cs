using Morphant.Exceptions;

namespace Morphant;

public abstract class AutoMarker
{
    private AutoMarker()
    {
    }
}

public abstract class AutoMarker<T>
{
    private AutoMarker()
    {
    }
}

public abstract class IgnoreMarker
{
    private IgnoreMarker()
    {
    }
}

public abstract class IgnoreMarker<T>
{
    private IgnoreMarker()
    {
    }
}

public abstract class ConstructorMarker
{
    private ConstructorMarker()
    {
    }

    public static implicit operator ConstructorMarker(AutoMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();
}
