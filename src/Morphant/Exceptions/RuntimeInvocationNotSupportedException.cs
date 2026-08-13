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
            "This Morphant configuration method cannot be called at " +
            "runtime. Use it only inside Configure.")
    {
    }
}
