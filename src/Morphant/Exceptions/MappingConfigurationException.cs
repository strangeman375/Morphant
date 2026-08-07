namespace Morphant.Exceptions;

/// <summary>
/// Represents a mapping configuration that Morphant could not generate.
/// </summary>
public sealed class MappingConfigurationException : MorphantException
{
    public MappingConfigurationException(
        Type sourceType,
        Type destinationType,
        string reason)
        : base(
            $"Mapping from '{sourceType}' to '{destinationType}' could not " +
            $"be generated. {reason}")
    {
    }
}
