namespace Morphant;

/// <summary>
/// Specifies how Morphant selects a constructor for convention-based
/// destination creation.
/// </summary>
public enum ConstructorSelection
{
    /// <summary>
    /// Inherits the setting. The fallback is <see cref="Unambiguous"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Disables automatic constructor selection.
    /// </summary>
    Explicit,

    /// <summary>
    /// Selects the supported parameterless constructor.
    /// </summary>
    Parameterless,

    /// <summary>
    /// Selects the constructor when exactly one supported constructor exists.
    /// </summary>
    Single,

    /// <summary>
    /// Selects the only parameterized constructor, or the parameterless one
    /// when no parameterized constructor exists.
    /// </summary>
    Unambiguous,

    /// <summary>
    /// Selects the unique applicable constructor with the most mapped
    /// arguments.
    /// </summary>
    Greediest,

    /// <summary>
    /// Selects the unique constructor with the most declared parameters, then
    /// requires it to be applicable.
    /// </summary>
    Largest
}
