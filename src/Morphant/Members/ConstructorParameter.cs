using Morphant.Exceptions;
using Morphant.Markers;

namespace Morphant.Members;

/// <summary>
/// Defines mapping for a constructor argument of type
/// <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The argument type.</typeparam>
public sealed class ConstructorParameter<T>
{
    private ConstructorParameter()
    {
    }

    /// <summary>
    /// Maps the argument from an explicit value.
    /// </summary>
    /// <param name="value">The value expression.</param>
    public static implicit operator ConstructorParameter<T>(T value) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Uses convention-based mapping.
    /// </summary>
    /// <param name="marker">The convention marker.</param>
    public static implicit operator ConstructorParameter<T>(AutoMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Uses convention-based mapping.
    /// </summary>
    /// <param name="marker">The convention marker.</param>
    public static implicit operator ConstructorParameter<T>(AutoMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Skips the argument.
    /// </summary>
    /// <param name="marker">The ignore marker.</param>
    public static implicit operator ConstructorParameter<T>(IgnoreMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Skips the argument.
    /// </summary>
    /// <param name="marker">The ignore marker.</param>
    public static implicit operator ConstructorParameter<T>(IgnoreMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Uses a nested mapping.
    /// </summary>
    /// <param name="marker">The nested-mapping marker.</param>
    public static implicit operator ConstructorParameter<T>(MapMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Uses an explicitly typed value.
    /// </summary>
    /// <param name="marker">The value marker.</param>
    public static implicit operator ConstructorParameter<T>(ValueMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();
}
