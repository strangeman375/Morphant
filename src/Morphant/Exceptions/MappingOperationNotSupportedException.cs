using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Thrown when the mapping mode disables the requested operation.
/// </summary>
public sealed class MappingOperationNotSupportedException : MappingException
{
    /// <summary>
    /// Initializes the exception for the specified mapping.
    /// </summary>
    /// <param name="operation">The requested operation.</param>
    /// <param name="sourceType">The source type.</param>
    /// <param name="destinationType">The destination type.</param>
    /// <param name="effectiveMappingMode">The effective mapping mode.</param>
    public MappingOperationNotSupportedException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType,
        MappingMode effectiveMappingMode)
        : base(
            $"MappingMode.{effectiveMappingMode} does not support " +
            $"{operation} for mapping '{sourceType}' -> " +
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
