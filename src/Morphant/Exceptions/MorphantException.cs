namespace Morphant.Exceptions;

/// <summary>
/// Represents a failure reported by Morphant.
/// </summary>
public abstract class MorphantException : Exception
{
    protected MorphantException(string message)
        : base(message)
    {
    }
}
