using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Represents a null destination rejected by the effective null-destination
/// policy.
/// </summary>
public sealed class NullDestinationException : MappingException
{
    public NullDestinationException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType)
        : base(
            $"The destination is null for mapping from '{sourceType}' to " +
            $"'{destinationType}', while the effective " +
            "NullDestinationHandling is Throw.",
            operation,
            sourceType,
            destinationType)
    {
    }
}
