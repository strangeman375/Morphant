using Morphant.Exceptions;

namespace Morphant;

/// <summary>
/// Specifies how a polymorphic mapping handles an unknown derived runtime
/// source type.
/// </summary>
public enum UnknownDerivedTypeHandling
{
    /// <summary>
    /// Inherits the setting. The fallback is <see cref="UseBaseMapping"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Uses the requested base mapping when no derived branch matches.
    /// </summary>
    UseBaseMapping,

    /// <summary>
    /// Throws <see cref="UnmatchedPolymorphicMappingException"/> when no
    /// derived branch matches.
    /// </summary>
    Throw
}
