namespace Morphant.Exceptions;

/// <summary>
/// Represents an attempt to read a default-initialized mapping context.
/// </summary>
public sealed class InvalidMappingContextException : MorphantException
{
    public InvalidMappingContextException()
        : base(
            "The mapping context is not initialized. Invoke the mapper " +
            "through IMapper or the context-free ITypeMapper Create/Update " +
            "extension methods before reading context data.")
    {
    }
}
