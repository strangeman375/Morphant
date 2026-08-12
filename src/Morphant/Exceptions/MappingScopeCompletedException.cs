using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Thrown when a completed mapping scope is reused.
/// </summary>
public sealed class MappingScopeCompletedException : MappingException
{
    /// <summary>
    /// Initializes the exception for the specified mapping.
    /// </summary>
    /// <param name="operation">The requested operation.</param>
    /// <param name="sourceType">The source type.</param>
    /// <param name="destinationType">The destination type.</param>
    public MappingScopeCompletedException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType)
        : base(
            "The mapping scope has already completed; mapping from " +
            $"'{sourceType}' to '{destinationType}' cannot start.",
            operation,
            sourceType,
            destinationType)
    {
    }
}
