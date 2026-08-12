namespace Morphant.Exceptions;

/// <summary>
/// Base class for exceptions reported by Morphant.
/// </summary>
public abstract class MorphantException : Exception
{
    private protected MorphantException(string message)
        : base(message)
    {
    }
}
