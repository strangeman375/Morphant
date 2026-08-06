namespace Morphant;

/// <summary>
/// Specifies how Morphant selects a constructor for convention-based
/// destination creation.
/// </summary>
public enum ConstructorSelection
{
    /// <summary>
    /// Inherits the next less specific setting. If every configured level
    /// inherits, Morphant uses <see cref="Unambiguous"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Disables automatic constructor selection. Creation requires an
    /// explicit <c>Construct</c> branch that does not use
    /// <c>ByConvention</c>.
    /// </summary>
    Explicit,

    /// <summary>
    /// Selects the supported parameterless constructor.
    /// </summary>
    Parameterless,

    /// <summary>
    /// Selects a constructor only when the destination has exactly one
    /// supported constructor.
    /// </summary>
    Single,

    /// <summary>
    /// Selects the only supported parameterized constructor, or the
    /// parameterless constructor when no parameterized constructor exists.
    /// Multiple supported parameterized constructors are ambiguous.
    /// </summary>
    Unambiguous,

    /// <summary>
    /// Selects the unique applicable constructor receiving the greatest
    /// number of mapped arguments. Omitted optional and <c>params</c>
    /// parameters do not count.
    /// </summary>
    Greediest,

    /// <summary>
    /// Selects the unique supported constructor with the greatest number of
    /// declared parameters, before checking whether it is applicable.
    /// </summary>
    Largest
}
