namespace Morphant.Exceptions;

/// <summary>
/// Represents a source-generator DSL API invoked directly at runtime.
/// </summary>
public sealed class RuntimeInvocationNotSupportedException : MorphantException
{
    public RuntimeInvocationNotSupportedException()
        : base(
            "This API is intended for use by source generators only and must " +
            "not be invoked at runtime.")
    {
    }
}
