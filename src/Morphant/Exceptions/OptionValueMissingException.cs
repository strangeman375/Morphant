namespace Morphant.Exceptions;

/// <summary>
/// Represents an attempt to read the value of an empty option.
/// </summary>
public sealed class OptionValueMissingException : MorphantException
{
    public OptionValueMissingException()
        : base("Option contains no value.")
    {
    }
}
