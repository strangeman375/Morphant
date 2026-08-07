namespace Morphant.Exceptions;

/// <summary>
/// Represents a declarative switch for which no branch matched.
/// </summary>
public sealed class UnmatchedMappingSwitchException : MorphantException
{
    public UnmatchedMappingSwitchException()
        : base(
            "No branch of the declarative mapping switch matched the " +
            "runtime value.")
    {
    }
}
