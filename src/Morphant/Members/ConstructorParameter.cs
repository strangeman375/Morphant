using Morphant.Exceptions;
using Morphant.Markers;

namespace Morphant.Members;

public abstract class ConstructorParameter<T>
{
    private ConstructorParameter()
    {
    }

    public static implicit operator ConstructorParameter<T>(T value) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator ConstructorParameter<T>(AutoMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator ConstructorParameter<T>(AutoMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator ConstructorParameter<T>(IgnoreMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator ConstructorParameter<T>(IgnoreMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator ConstructorParameter<T>(MapMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    public static implicit operator ConstructorParameter<T>(MapMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();
}
