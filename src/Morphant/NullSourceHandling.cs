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
    /// Reference types return null regardless of nullable annotations.
    /// </remarks>
    ReturnNull,

    /// <summary>
    /// Returns the supplied destination on Update, or
    /// <see langword="default"/> on Create.
    /// </summary>
    ReturnDestination,

    /// <summary>
    /// Throws <see cref="NullSourceException"/>.
    /// </summary>
    Throw
}
