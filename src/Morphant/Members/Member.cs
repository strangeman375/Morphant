using Morphant.Exceptions;
using Morphant.Markers;

namespace Morphant.Members;

/// <summary>
/// Defines mapping for a destination member of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The member type.</typeparam>
public sealed class Member<T>
{
    private Member()
    {
    }

    /// <summary>
    /// Maps the member from an explicit value.
    /// </summary>
    /// <param name="value">The value expression.</param>
    public static implicit operator Member<T>(T value) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Uses convention-based mapping.
    /// </summary>
    /// <param name="marker">The convention marker.</param>
    public static implicit operator Member<T>(AutoMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Uses convention-based mapping.
    /// </summary>
    /// <param name="marker">The convention marker.</param>
    public static implicit operator Member<T>(AutoMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Skips the member.
    /// </summary>
    /// <param name="marker">The ignore marker.</param>
    public static implicit operator Member<T>(IgnoreMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Skips the member.
    /// </summary>
    /// <param name="marker">The ignore marker.</param>
    public static implicit operator Member<T>(IgnoreMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Uses a nested mapping.
    /// </summary>
    /// <param name="marker">The nested-mapping marker.</param>
    public static implicit operator Member<T>(MapMarker marker) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Uses an explicitly typed value.
    /// </summary>
    /// <param name="marker">The value marker.</param>
    public static implicit operator Member<T>(ValueMarker<T> marker) =>
        throw new RuntimeInvocationNotSupportedException();
}
