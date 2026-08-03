namespace Morphant;

/// <summary>
/// Specifies how mapping to an existing destination handles a
/// <see langword="null"/> destination.
/// </summary>
public enum NullDestinationHandling
{
    /// <summary>
    /// Inherits the next less specific setting. If no level specifies a
    /// value, Morphant uses <see cref="Create"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Creates and maps a new destination.
    /// </summary>
    /// <remarks>
    /// Morphant runs the no-previous construction branch while preserving the
    /// current <see cref="MappingOperation.Update"/> operation.
    /// </remarks>
    Create,

    /// <summary>
    /// Throws <see cref="ArgumentNullException"/>.
    /// </summary>
    Throw
}
