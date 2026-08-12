using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Thrown when a mapping configuration cannot be generated.
/// </summary>
public sealed class MappingConfigurationException : MappingException
{
    /// <summary>
    /// Initializes the exception for the specified mapping.
    /// </summary>
    /// <param name="operation">The requested operation.</param>
    /// <param name="sourceType">The source type.</param>
    /// <param name="destinationType">The destination type.</param>
    /// <param name="reason">The failure reason.</param>
    public MappingConfigurationException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType,
        string reason)
        : base(
            $"Mapping from '{sourceType}' to '{destinationType}' could not " +
            $"be generated. {reason}",
            operation,
            sourceType,
            destinationType)
    {
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }

    /// <summary>
    /// Gets the reason the mapping could not be generated.
    /// </summary>
    public string Reason { get; }
}
