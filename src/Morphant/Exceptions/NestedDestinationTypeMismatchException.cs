namespace Morphant.Exceptions;

/// <summary>
/// Represents a current nested destination whose runtime type is incompatible
/// with the explicitly requested nested destination type.
/// </summary>
public sealed class NestedDestinationTypeMismatchException : MorphantException
{
    public NestedDestinationTypeMismatchException(
        Type expectedType,
        Type? actualType)
        : base(
            actualType is null
                ? $"The current nested destination is null and cannot be " +
                  $"used as '{expectedType}'."
                : $"The current nested destination has runtime type " +
                  $"'{actualType}', which cannot be used as " +
                  $"'{expectedType}'.")
    {
    }
}
