using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Thrown when a nested destination has an incompatible runtime type.
/// </summary>
public sealed class NestedDestinationTypeMismatchException : MappingException
{
    /// <summary>
    /// Creates the exception for a generated nested mapping call.
    /// </summary>
    /// <typeparam name="TSource">The nested mapping source type.</typeparam>
    /// <typeparam name="TDestination">The nested mapping destination
    /// type.</typeparam>
    /// <param name="operation">The requested operation.</param>
    /// <param name="destination">The incompatible destination, or
    /// <see langword="null"/>.</param>
    /// <returns>The initialized exception.</returns>
    public static NestedDestinationTypeMismatchException
        Create<TSource, TDestination>(
            MappingOperation operation,
            object? destination) =>
        new(
            operation,
            typeof(TSource),
            typeof(TDestination),
            typeof(TDestination),
            destination?.GetType());

    /// <summary>
    /// Initializes the exception for the specified nested mapping.
    /// </summary>
    /// <param name="operation">The requested operation.</param>
    /// <param name="sourceType">The source type.</param>
    /// <param name="destinationType">The destination type.</param>
    /// <param name="expectedDestinationType">The required nested destination
    /// type.</param>
    /// <param name="actualDestinationType">The actual runtime type, or
    /// <see langword="null"/>.</param>
    public NestedDestinationTypeMismatchException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType,
        Type expectedDestinationType,
        Type? actualDestinationType)
        : base(
            actualDestinationType is null
                ? $"The current destination is null and cannot be used as " +
                  $"'{expectedDestinationType}'."
                : $"Current destination type '{actualDestinationType}' " +
                  $"cannot be used as " +
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
    /// Gets the incompatible runtime type, or <see langword="null"/>.
    /// </summary>
    public Type? ActualDestinationType { get; }
}
