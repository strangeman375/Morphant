namespace Morphant.Exceptions;

public sealed class RuntimeInvocationNotSupportedException : NotSupportedException
{
    public RuntimeInvocationNotSupportedException()
        : base("This API is intended for use by source generators only and must not be invoked at runtime.")
    {
    }
}
