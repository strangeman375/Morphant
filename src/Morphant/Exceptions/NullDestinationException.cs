namespace Morphant.Exceptions;

/// <summary>
/// Represents a null destination rejected by the effective null-destination
/// policy.
/// </summary>
public sealed class NullDestinationException : MorphantException
{
    public NullDestinationException(Type sourceType, Type destinationType)
        : base(
            $"The destination is null for mapping from '{sourceType}' to " +
            $"'{destinationType}', while the effective " +
            "NullDestinationHandling is Throw.")
    {
    }
}
