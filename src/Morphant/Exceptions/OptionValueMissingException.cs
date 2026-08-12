namespace Morphant.Exceptions;

/// <summary>
/// Thrown when the value of an empty option is read.
/// </summary>
public sealed class OptionValueMissingException : MorphantException
{
    /// <summary>
    /// Initializes the exception.
    /// </summary>
    public OptionValueMissingException()
        : base("Option contains no value.")
    {
    }
}
