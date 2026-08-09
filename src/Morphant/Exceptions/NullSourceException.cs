using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Represents a null source rejected by the effective null-source policy.
/// </summary>
public sealed class NullSourceException : MappingException
{
    public NullSourceException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType)
        : base(
            $"The source is null for mapping from '{sourceType}' to " +
            $"'{destinationType}', while the effective " +
            "NullSourceHandling is Throw.",
            operation,
            sourceType,
            destinationType)
    {
    }
}
