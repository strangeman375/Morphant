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
    /// Morphant runs the new-destination mapping plan. A source-only template
    /// participates when configured; a destination-aware template is not
    /// invoked.
    /// </remarks>
    CreateNew,

    /// <summary>
    /// Throws <see cref="ArgumentNullException"/>.
    /// </summary>
    Throw
}
