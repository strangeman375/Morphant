using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Thrown when an Update destination cannot be passed to the selected
/// polymorphic branch.
/// </summary>
public sealed class PolymorphicDestinationTypeMismatchException :
    MappingException
{
    /// <summary>
    /// Initializes the exception for the requested base mapping.
    /// </summary>
    /// <param name="operation">The requested operation.</param>
    /// <param name="sourceType">The requested base source type.</param>
    /// <param name="destinationType">The requested base destination type.</param>
    /// <param name="actualSourceType">The actual runtime source type.</param>
    /// <param name="branchSourceType">The selected branch source type.</param>
    /// <param name="expectedDestinationType">The selected branch destination
    /// type.</param>
    /// <param name="actualDestinationType">The actual destination runtime
    /// type, or <see langword="null"/>.</param>
    public PolymorphicDestinationTypeMismatchException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType,
        Type actualSourceType,
        Type branchSourceType,
        Type expectedDestinationType,
        Type? actualDestinationType)
        : base(
            actualDestinationType is null
                ? $"Runtime source type '{actualSourceType}' selected " +
                  $"polymorphic branch '{branchSourceType}' -> " +
                  $"'{expectedDestinationType}', but a null destination " +
                  $"cannot be used as '{expectedDestinationType}'."
                : $"Runtime source type '{actualSourceType}' selected " +
                  $"polymorphic branch '{branchSourceType}' -> " +
                  $"'{expectedDestinationType}', but destination type " +
                  $"'{actualDestinationType}' cannot be used as " +
                  $"'{expectedDestinationType}'.",
            operation,
            sourceType,
            destinationType)
    {
        ActualSourceType = actualSourceType ??
            throw new ArgumentNullException(nameof(actualSourceType));
        BranchSourceType = branchSourceType ??
            throw new ArgumentNullException(nameof(branchSourceType));
        ExpectedDestinationType = expectedDestinationType ??
            throw new ArgumentNullException(
                nameof(expectedDestinationType));
        ActualDestinationType = actualDestinationType;
    }

    /// <summary>
    /// Gets the runtime source type that selected the branch.
    /// </summary>
    public Type ActualSourceType { get; }

    /// <summary>
    /// Gets the source type of the selected branch.
    /// </summary>
    public Type BranchSourceType { get; }

    /// <summary>
    /// Gets the destination type required by the selected branch.
    /// </summary>
    public Type ExpectedDestinationType { get; }

    /// <summary>
    /// Gets the incompatible destination runtime type, or
    /// <see langword="null"/>.
    /// </summary>
    public Type? ActualDestinationType { get; }
}
