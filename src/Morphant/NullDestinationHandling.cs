using Morphant.Context;

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
    /// Treats the <see langword="null"/> destination as absent and runs the
    /// no-previous construction branch.
    /// </summary>
    /// <remarks>
    /// Morphant runs the no-previous construction branch while preserving the
    /// current <see cref="MappingOperation.Update"/> operation. The effective
    /// <see cref="MappingMode"/> only needs to include
    /// <see cref="MappingMode.Update"/>; <see cref="MappingMode.Create"/> is
    /// not required.
    /// </remarks>
    Create,

    /// <summary>
    /// Throws <see cref="ArgumentNullException"/>.
    /// </summary>
    Throw
}
