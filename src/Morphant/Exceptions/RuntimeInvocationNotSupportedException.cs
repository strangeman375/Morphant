namespace Morphant.Exceptions;

/// <summary>
/// Thrown when a compile-time DSL API is invoked at runtime.
/// </summary>
public sealed class RuntimeInvocationNotSupportedException : MorphantException
{
    /// <summary>
    /// Initializes the exception.
    /// </summary>
    public RuntimeInvocationNotSupportedException()
        : base(
            "This API is intended for use by source generators only and must " +
            "not be invoked at runtime.")
    {
    }
}
