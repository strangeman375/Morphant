using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Represents an invalid runtime mapping registration.
/// </summary>
public sealed class InvalidMappingRegistrationException : MappingException
{
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
