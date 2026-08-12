using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Thrown when no branch of a declarative switch matches.
/// </summary>
public sealed class UnmatchedMappingSwitchException : MappingException
{
    /// <summary>
    /// Initializes the exception for the specified mapping.
    /// </summary>
    /// <param name="operation">The requested operation.</param>
    /// <param name="sourceType">The source type.</param>
    /// <param name="destinationType">The destination type.</param>
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
