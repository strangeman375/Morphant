namespace Morphant.Exceptions;

/// <summary>
/// Thrown when a default-initialized mapping context is read.
/// </summary>
public sealed class InvalidMappingContextException : MorphantException
{
    /// <summary>
    /// Initializes the exception.
    /// </summary>
    public InvalidMappingContextException()
        : base(
            "MappingContext is not initialized. Use IMapper or the " +
            "ITypeMapper Create/Update extension methods.")
    {
    }
}
