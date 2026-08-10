namespace Morphant.Markers;

/// <summary>
/// Represents an explicit declarative value whose final target type is
/// <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The exact final value type.</typeparam>
/// <remarks>
/// This type exists only for compile-time binding of generated mapping plans.
/// Instances are not created or used by a generated mapper at runtime.
/// </remarks>
public sealed class ValueMarker<T>
{
    private ValueMarker()
    {
    }
}
