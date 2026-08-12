using Morphant.Exceptions;

namespace Morphant;

/// <summary>
/// Specifies how a mapping handles a <see langword="null"/> source.
/// </summary>
public enum NullSourceHandling
{
    /// <summary>
    /// Inherits the setting. The fallback is <see cref="ReturnNull"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Returns <see langword="default"/> for the destination type.
    /// </summary>
    /// <remarks>
    /// The result is <see langword="null"/> when the destination type permits
    /// it.
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
