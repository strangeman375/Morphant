namespace Morphant.Exceptions;

/// <summary>
/// Represents a mapping lookup with more than one registered candidate.
/// </summary>
public sealed class AmbiguousMappingException : MorphantException
{
    public AmbiguousMappingException(Type sourceType, Type destinationType)
        : base(
            $"Multiple mappings are registered from '{sourceType}' to " +
            $"'{destinationType}'. Exactly one mapping is required.")
    {
    }
}
