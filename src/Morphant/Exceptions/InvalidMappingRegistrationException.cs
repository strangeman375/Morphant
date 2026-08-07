namespace Morphant.Exceptions;

/// <summary>
/// Represents an invalid runtime mapping registration.
/// </summary>
public sealed class InvalidMappingRegistrationException : MorphantException
{
    public InvalidMappingRegistrationException(
        Type sourceType,
        Type destinationType)
        : base(
            $"The registered mapping from '{sourceType}' to " +
            $"'{destinationType}' resolved to null.")
    {
    }
}
