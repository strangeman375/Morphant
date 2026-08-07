using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Represents a mapping operation disabled by the effective mapping mode.
/// </summary>
public sealed class MappingOperationNotSupportedException : MorphantException
{
    public MappingOperationNotSupportedException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType)
        : base(
            $"The {operation} operation is disabled by the effective " +
            $"MappingMode for mapping from '{sourceType}' to " +
            $"'{destinationType}'.")
    {
    }
}
