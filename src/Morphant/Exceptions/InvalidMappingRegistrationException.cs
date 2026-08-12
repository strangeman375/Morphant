using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Thrown when a mapping registration resolves to <see langword="null"/>.
/// </summary>
public sealed class InvalidMappingRegistrationException : MappingException
{
    /// <summary>
    /// Initializes the exception for the specified mapping.
    /// </summary>
    /// <param name="operation">The requested operation.</param>
    /// <param name="sourceType">The source type.</param>
    /// <param name="destinationType">The destination type.</param>
    public InvalidMappingRegistrationException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType)
        : base(
            $"The registered mapping from '{sourceType}' to " +
            $"'{destinationType}' resolved to null.",
            operation,
            sourceType,
            destinationType)
    {
    }
}
