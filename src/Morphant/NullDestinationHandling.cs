namespace Morphant;

/// <summary>
/// Specifies how mapping to an existing destination handles a
/// <see langword="null"/> destination.
/// </summary>
public enum NullDestinationHandling
{
    /// <summary>
    /// Inherits the next less specific setting. If no level specifies a
    /// value, Morphant uses <see cref="CreateNew"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Creates and maps a new destination.
    /// </summary>
    /// <remarks>
    /// A template that receives the destination's previous state observes the
    /// original <see langword="null"/> or <see langword="default"/> value.
    /// </remarks>
    CreateNew,

    /// <summary>
    /// Throws <see cref="ArgumentNullException"/>.
    /// </summary>
    Throw
}
