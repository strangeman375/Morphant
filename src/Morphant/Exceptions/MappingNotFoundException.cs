using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Represents a mapping lookup with no registered candidate.
/// </summary>
public sealed class MappingNotFoundException : MappingException
{
    public MappingNotFoundException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType)
        : this(
            operation,
            sourceType,
            destinationType,
            "No mapping is registered",
            string.Empty)
    {
    }

    private MappingNotFoundException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType,
        string prefix,
        string guidance)
        : base(
            $"{prefix} from '{sourceType}' to '{destinationType}'." +
            guidance,
            operation,
            sourceType,
            destinationType)
    {
    }

    internal static MappingNotFoundException ForStandalone(
        MappingOperation operation,
        Type sourceType,
        Type destinationType) =>
        new(
            operation,
            sourceType,
            destinationType,
            "The standalone mapper instance does not implement a mapping",
            " Use IMapper when a nested mapping belongs to another mapper " +
            "instance.");
}
