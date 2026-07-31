namespace Morphant;

/// <summary>
/// Specifies how a mapping's <c>Template()</c> lambda is interpreted.
/// </summary>
/// <remarks>
/// <para>
/// A mapping-level <see cref="Default"/> value inherits the mapper-level
/// setting. A mapper-level <see cref="Default"/> value inherits the
/// assembly-level <c>MorphantTemplateMode</c> MSBuild property. If no level
/// specifies a value, Morphant uses <see cref="Dsl"/>.
/// </para>
/// <para>
/// The effective value belongs to the mapping's source and destination type
/// pair. When mappings to the same destination use different values, Morphant
/// generates pair-specific <c>Template()</c> extension methods.
/// </para>
/// </remarks>
public enum TemplateMode
{
    /// <summary>
    /// Inherits the mode from the enclosing configuration level.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Interprets the lambda through the Morphant template DSL and applies the
    /// remaining effective mapping rules.
    /// </summary>
    /// <remarks>
    /// Morphant generates a destination template type when the destination
    /// supports one. For direct-only destinations, such as built-in scalar
    /// types, the lambda returns the destination value directly.
    /// </remarks>
    Dsl,

    /// <summary>
    /// Uses the destination returned by the lambda as the final mapping result.
    /// </summary>
    /// <remarks>
    /// Morphant does not apply constructor or member mappings to the returned
    /// value. When mapping to an existing destination, the lambda may return
    /// the supplied destination or replace it with another instance.
    /// </remarks>
    Raw
}
