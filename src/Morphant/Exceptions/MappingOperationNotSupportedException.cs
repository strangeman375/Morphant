using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Represents a mapping operation disabled by the effective mapping mode.
/// </summary>
public sealed class MappingOperationNotSupportedException : MappingException
{
    public MappingOperationNotSupportedException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType,
        MappingMode effectiveMappingMode)
        : base(
            $"The {operation} operation is disabled by the effective " +
            $"MappingMode for mapping from '{sourceType}' to " +
            $"'{destinationType}'.",
            operation,
            sourceType,
            destinationType)
    {
        EffectiveMappingMode = effectiveMappingMode;
    }

    /// <summary>
    /// Gets the effective mapping mode that disabled the requested operation.
    /// </summary>
    public MappingMode EffectiveMappingMode { get; }
}
