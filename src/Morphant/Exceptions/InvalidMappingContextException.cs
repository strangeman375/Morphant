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
            "The mapping context is not initialized. Invoke the mapper " +
            "through IMapper or the context-free ITypeMapper Create/Update " +
            "extension methods before reading context data.")
    {
    }
}
