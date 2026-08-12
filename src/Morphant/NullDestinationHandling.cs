using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant;

/// <summary>
/// Specifies how mapping to an existing destination handles a
/// <see langword="null"/> destination.
/// </summary>
public enum NullDestinationHandling
{
    /// <summary>
    /// Inherits the setting. The fallback is <see cref="Create"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Creates a destination when the supplied destination is
    /// <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// The operation remains <see cref="MappingOperation.Update"/> and does
    /// not require <see cref="MappingMode.Create"/>.
    /// </remarks>
    Create,

    /// <summary>
    /// Throws <see cref="NullDestinationException"/>.
    /// </summary>
    Throw
}
