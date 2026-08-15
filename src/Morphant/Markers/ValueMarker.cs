using System.Diagnostics.CodeAnalysis;

namespace Morphant.Markers;

/// <summary>
/// Marks an explicit declarative value whose target type is
/// <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The exact final value type.</typeparam>
/// <remarks>
/// Used only for compile-time binding; no runtime instance is created.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed class ValueMarker<T>
{
    private ValueMarker()
    {
    }
}
