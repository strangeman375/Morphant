using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Thrown when the null-destination policy rejects a destination.
/// </summary>
public sealed class NullDestinationException : MappingException
{
    /// <summary>
    /// Initializes the exception for the specified mapping.
    /// </summary>
    /// <param name="operation">The requested operation.</param>
    /// <param name="sourceType">The source type.</param>
    /// <param name="destinationType">The destination type.</param>
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
