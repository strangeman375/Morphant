namespace Morphant;

/// <summary>
/// Specifies which mapping operations a generated type mapper supports.
/// </summary>
/// <remarks>
/// <para>
/// A mapping-level <see cref="Default"/> value inherits the mapper-level
/// setting. If neither level specifies a mode, the effective mode is
/// <see cref="MapNewAndExisting"/>.
/// </para>
/// <para>
/// A generated <see cref="ITypeMapper{TSource, TDestination}"/> always
/// implements both mapping overloads. An overload excluded by the effective
/// mode throws <see cref="NotSupportedException"/> when invoked.
/// </para>
/// </remarks>
[Flags]
public enum MappingMode
{
    /// <summary>
    /// Inherits the mode from the enclosing configuration level.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Supports mapping a source to a new destination.
    /// </summary>
    MapNew = 1 << 0,

    /// <summary>
    /// Supports mapping a source to an existing destination.
    /// </summary>
    MapExisting = 1 << 1,

    /// <summary>
    /// Supports mapping both to a new destination and to an existing
    /// destination.
    /// </summary>
    MapNewAndExisting = MapNew | MapExisting
}
