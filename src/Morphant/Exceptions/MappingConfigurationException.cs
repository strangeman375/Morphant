using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Represents a mapping configuration that Morphant could not generate.
/// </summary>
public sealed class MappingConfigurationException : MappingException
{
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
    /// Gets the human-readable reason the mapping could not be generated.
    /// </summary>
    public string Reason { get; }
}
