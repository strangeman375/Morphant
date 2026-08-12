using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Thrown when more than one mapping matches a request.
/// </summary>
public sealed class AmbiguousMappingException : MappingException
{
    /// <summary>
    /// Initializes the exception for the specified mapping.
    /// </summary>
    /// <param name="operation">The requested operation.</param>
    /// <param name="sourceType">The source type.</param>
    /// <param name="destinationType">The destination type.</param>
    public AmbiguousMappingException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType)
        : base(
            $"Multiple mappings are registered from '{sourceType}' to " +
            $"'{destinationType}'. Exactly one mapping is required.",
            operation,
            sourceType,
            destinationType)
    {
    }
}
