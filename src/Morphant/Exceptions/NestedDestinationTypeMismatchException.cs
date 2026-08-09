using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Represents a current nested destination whose runtime type is incompatible
/// with the explicitly requested nested destination type.
/// </summary>
public sealed class NestedDestinationTypeMismatchException : MappingException
{
    public NestedDestinationTypeMismatchException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType,
        Type expectedDestinationType,
        Type? actualDestinationType)
        : base(
            actualDestinationType is null
                ? $"The current nested destination is null and cannot be " +
                  $"used as '{expectedDestinationType}'."
                : $"The current nested destination has runtime type " +
                  $"'{actualDestinationType}', which cannot be used as " +
                  $"'{expectedDestinationType}'.",
            operation,
            sourceType,
            destinationType)
    {
        ExpectedDestinationType = expectedDestinationType ??
            throw new ArgumentNullException(
                nameof(expectedDestinationType));
        ActualDestinationType = actualDestinationType;
    }

    /// <summary>
    /// Gets the destination type required by the nested mapping call.
    /// </summary>
    public Type ExpectedDestinationType { get; }

    /// <summary>
    /// Gets the incompatible runtime type, or <see langword="null"/> when the
    /// current nested destination was null.
    /// </summary>
    public Type? ActualDestinationType { get; }
}
