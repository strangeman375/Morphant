using Morphant.Exceptions;

namespace Morphant.Markers;

/// <summary>
/// Base type for declarative member markers.
/// </summary>
public abstract class MemberMarker
{
    private protected MemberMarker()
    {
    }
}

/// <summary>
/// Selects convention-based mapping for the current target.
/// </summary>
public sealed class AutoMarker : MemberMarker
{
    private AutoMarker()
    {
    }
}

/// <summary>
/// Selects convention-based mapping to <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The target type.</typeparam>
public sealed class AutoMarker<T> : MemberMarker
{
    private AutoMarker()
    {
    }

    /// <summary>
    /// Supports target-typed declarative expressions.
    /// </summary>
    /// <param name="value">The value expression.</param>
    public static implicit operator AutoMarker<T>(T value) =>
        throw new RuntimeInvocationNotSupportedException();
}

/// <summary>
/// Skips the current member or constructor argument.
/// </summary>
public sealed class IgnoreMarker : MemberMarker
{
    private IgnoreMarker()
    {
    }
}

/// <summary>
/// Skips a target of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The target type.</typeparam>
public sealed class IgnoreMarker<T> : MemberMarker
{
    private IgnoreMarker()
    {
    }

    /// <summary>
    /// Supports target-typed declarative expressions.
    /// </summary>
    /// <param name="value">The value expression.</param>
    public static implicit operator IgnoreMarker<T>(T value) =>
        throw new RuntimeInvocationNotSupportedException();
}

/// <summary>
/// Base type for declarative nested-mapping markers.
/// </summary>
public abstract class MapMarker : MemberMarker
{
    private protected MapMarker()
    {
    }
}

/// <summary>
/// Selects a nested mapping to <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The nested destination type.</typeparam>
public sealed class MapMarker<T> : MapMarker
{
    private MapMarker()
    {
    }

    /// <summary>
    /// Supports target-typed declarative expressions.
    /// </summary>
    /// <param name="value">The value expression.</param>
    public static implicit operator MapMarker<T>(T value) =>
        throw new RuntimeInvocationNotSupportedException();
}
