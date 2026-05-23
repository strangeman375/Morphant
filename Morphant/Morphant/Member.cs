using Morphant.Exceptions;

namespace Morphant;

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
