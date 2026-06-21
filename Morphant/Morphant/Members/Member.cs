using Morphant.Exceptions;
using Morphant.Markers;

namespace Morphant.Members;

public abstract class Member<T>
{
    private Member()
    {
    }

    public static implicit operator Member<T>(T value) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator Member<T>(AutoMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator Member<T>(AutoMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator Member<T>(IgnoreMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator Member<T>(IgnoreMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator Member<T>(MapMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator Member<T>(MapMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();
}
