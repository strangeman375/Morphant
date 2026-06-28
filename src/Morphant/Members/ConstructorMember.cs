using Morphant.Exceptions;
using Morphant.Markers;

namespace Morphant.Members;

public abstract class ConstructorMember<T>
{
    private ConstructorMember()
    {
    }

    public static implicit operator ConstructorMember<T>(T value) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator ConstructorMember<T>(AutoMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator ConstructorMember<T>(AutoMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator ConstructorMember<T>(IgnoreMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator ConstructorMember<T>(IgnoreMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator ConstructorMember<T>(MapMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator ConstructorMember<T>(MapMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();
}
