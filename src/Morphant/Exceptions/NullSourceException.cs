namespace Morphant.Exceptions;

/// <summary>
/// Represents a null source rejected by the effective null-source policy.
/// </summary>
public sealed class NullSourceException : MorphantException
{
    public NullSourceException(Type sourceType, Type destinationType)
        : base(
            $"The source is null for mapping from '{sourceType}' to " +
            $"'{destinationType}', while the effective " +
            "NullSourceHandling is Throw.")
    {
    }
}
