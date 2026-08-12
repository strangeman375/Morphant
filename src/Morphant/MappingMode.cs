using Morphant.Exceptions;

namespace Morphant;

/// <summary>
/// Specifies which operations a mapping supports.
/// </summary>
/// <remarks>
/// Calling a disabled operation throws
/// <see cref="MappingOperationNotSupportedException"/>.
/// </remarks>
[Flags]
public enum MappingMode
{
    /// <summary>
    /// Inherits the setting. The fallback is <see cref="CreateAndUpdate"/>.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Supports mapping a source to a new destination.
    /// </summary>
    Create = 1 << 0,

    /// <summary>
    /// Supports mapping a source to an existing destination.
    /// </summary>
    Update = 1 << 1,

    /// <summary>
    /// Supports both creation and update.
    /// </summary>
    CreateAndUpdate = Create | Update
}
