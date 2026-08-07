namespace Morphant.Exceptions;

/// <summary>
/// Represents an attempt to reuse a completed mapping scope.
/// </summary>
public sealed class MappingScopeCompletedException : MorphantException
{
    public MappingScopeCompletedException(
        Type sourceType,
        Type destinationType)
        : base(
            "The mapping scope has already completed; mapping from " +
            $"'{sourceType}' to '{destinationType}' cannot start.")
    {
    }
}
