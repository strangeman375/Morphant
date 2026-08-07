using Morphant.Exceptions;

namespace Morphant;

/// <summary>
/// Specifies how a mapping handles a <see langword="null"/> source.
/// </summary>
public enum NullSourceHandling
{
    /// <summary>
    /// Inherits the next less specific setting. If no level specifies a
    /// value, Morphant uses <see cref="ReturnNull"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Returns <see langword="default"/> for the destination type.
    /// </summary>
    /// <remarks>
    /// The result is <see langword="null"/> for a reference or nullable value
    /// destination and <see langword="default"/> for a non-nullable value
    /// destination.
    /// </remarks>
    ReturnNull,

    /// <summary>
    /// Returns the supplied destination when mapping to an existing
    /// destination; when mapping to a new destination, returns
    /// <see langword="default"/>.
    /// </summary>
    ReturnDestination,

    /// <summary>
    /// Throws <see cref="NullSourceException"/>.
    /// </summary>
    Throw
}
