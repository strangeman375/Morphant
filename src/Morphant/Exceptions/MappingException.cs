using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Represents a failure associated with a concrete mapping operation and
/// exact source/destination pair.
/// </summary>
public abstract class MappingException : MorphantException
{
    private protected MappingException(
        string message,
        MappingOperation operation,
        Type sourceType,
        Type destinationType)
        : base(message)
    {
        Operation = operation;
        SourceType = sourceType ??
            throw new ArgumentNullException(nameof(sourceType));
        DestinationType = destinationType ??
            throw new ArgumentNullException(nameof(destinationType));
    }

    /// <summary>
    /// Gets the operation that failed.
    /// </summary>
    public MappingOperation Operation { get; }

    /// <summary>
    /// Gets the exact source type of the mapping pair.
    /// </summary>
    public Type SourceType { get; }

    /// <summary>
    /// Gets the exact destination type of the mapping pair.
    /// </summary>
    public Type DestinationType { get; }
}
