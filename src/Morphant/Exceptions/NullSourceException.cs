using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Thrown when the null-source policy rejects a source.
/// </summary>
public sealed class NullSourceException : MappingException
{
    /// <summary>
    /// Initializes the exception for the specified mapping.
    /// </summary>
    /// <param name="operation">The requested operation.</param>
    /// <param name="sourceType">The source type.</param>
    /// <param name="destinationType">The destination type.</param>
    public NullSourceException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType)
        : base(
            $"NullSourceHandling.Throw does not allow a null source for " +
            $"mapping '{sourceType}' -> '{destinationType}'.",
            operation,
            sourceType,
            destinationType)
    {
    }
}
