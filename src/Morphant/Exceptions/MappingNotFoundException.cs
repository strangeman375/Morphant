namespace Morphant.Exceptions;

/// <summary>
/// Represents a mapping lookup with no registered candidate.
/// </summary>
public sealed class MappingNotFoundException : MorphantException
{
    public MappingNotFoundException(Type sourceType, Type destinationType)
        : base(
            $"No mapping is registered from '{sourceType}' to " +
            $"'{destinationType}'.")
    {
    }
}
