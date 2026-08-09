using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Represents a declarative switch for which no branch matched.
/// </summary>
public sealed class UnmatchedMappingSwitchException : MappingException
{
    public UnmatchedMappingSwitchException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType)
        : base(
            "No branch of the declarative mapping switch matched the " +
            "runtime value.",
            operation,
            sourceType,
            destinationType)
    {
    }
}
