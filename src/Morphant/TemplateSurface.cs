namespace Morphant;

/// <summary>
/// Specifies the compile-time template API generated for a mapping.
/// </summary>
/// <remarks>
/// <para>
/// A mapping-level <see cref="Default"/> value inherits the mapper-level
/// setting. A mapper-level <see cref="Default"/> value inherits the
/// assembly-level <c>TemplateSurface</c> MSBuild property. If no level
/// specifies a value, Morphant uses <see cref="Full"/>.
/// </para>
/// <para>
/// The effective value belongs to the mapping's source and destination type
/// pair. When mappings to the same destination use different values, Morphant
/// generates pair-specific <c>Template()</c> extension methods.
/// </para>
/// </remarks>
public enum TemplateSurface
{
    /// <summary>
    /// Inherits the surface from the enclosing configuration level.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Generates a destination template type and <c>Template()</c> extension
    /// methods that use it.
    /// </summary>
    /// <remarks>
    /// Destination types that only support direct templates, such as built-in
    /// scalar types, use the direct surface instead.
    /// </remarks>
    Full,

    /// <summary>
    /// Generates <c>Template()</c> extension methods whose lambda returns the
    /// destination type directly, without generating a destination template
    /// type for this mapping.
    /// </summary>
    Direct,

    /// <summary>
    /// Does not generate <c>Template()</c> extension methods or request a
    /// destination template type for this mapping.
    /// </summary>
    None
}
