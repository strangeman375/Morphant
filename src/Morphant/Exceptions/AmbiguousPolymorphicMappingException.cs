using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Thrown when several incomparable polymorphic branches are equally
/// specific for a runtime source.
/// </summary>
public sealed class AmbiguousPolymorphicMappingException : MappingException
{
    /// <summary>
    /// Initializes the exception for the requested base mapping.
    /// </summary>
    /// <param name="operation">The requested operation.</param>
    /// <param name="sourceType">The requested base source type.</param>
    /// <param name="destinationType">The requested base destination type.</param>
    /// <param name="actualSourceType">The actual runtime source type.</param>
    /// <param name="matchingSourceTypes">The maximal matching branch source
    /// types.</param>
    /// <param name="matchingDestinationTypes">The corresponding branch
    /// destination types.</param>
    public AmbiguousPolymorphicMappingException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType,
        Type actualSourceType,
        Type[] matchingSourceTypes,
        Type[] matchingDestinationTypes)
        : base(
            BuildMessage(
                sourceType,
                destinationType,
                actualSourceType,
                matchingSourceTypes,
                matchingDestinationTypes),
            operation,
            sourceType,
            destinationType)
    {
        ActualSourceType = actualSourceType ??
            throw new ArgumentNullException(nameof(actualSourceType));
        MatchingSourceTypes = Array.AsReadOnly(
            (Type[])(matchingSourceTypes ??
                throw new ArgumentNullException(
                    nameof(matchingSourceTypes))).Clone());
        MatchingDestinationTypes = Array.AsReadOnly(
            (Type[])(matchingDestinationTypes ??
                throw new ArgumentNullException(
                    nameof(matchingDestinationTypes))).Clone());

        if (MatchingSourceTypes.Count != MatchingDestinationTypes.Count ||
            MatchingSourceTypes.Count < 2)
        {
            throw new ArgumentException(
                "At least two corresponding polymorphic branches are " +
                "required.");
        }
    }

    /// <summary>
    /// Gets the ambiguous runtime source type.
    /// </summary>
    public Type ActualSourceType { get; }

    /// <summary>
    /// Gets the source types of the maximal matching branches.
    /// </summary>
    public IReadOnlyList<Type> MatchingSourceTypes { get; }

    /// <summary>
    /// Gets the destination types of the corresponding matching branches.
    /// </summary>
    public IReadOnlyList<Type> MatchingDestinationTypes { get; }

    private static string BuildMessage(
        Type sourceType,
        Type destinationType,
        Type actualSourceType,
        Type[] matchingSourceTypes,
        Type[] matchingDestinationTypes)
    {
        if (actualSourceType is null)
        {
            throw new ArgumentNullException(nameof(actualSourceType));
        }

        if (matchingSourceTypes is null)
        {
            throw new ArgumentNullException(nameof(matchingSourceTypes));
        }

        if (matchingDestinationTypes is null)
        {
            throw new ArgumentNullException(nameof(matchingDestinationTypes));
        }

        var count = Math.Min(
            matchingSourceTypes.Length,
            matchingDestinationTypes.Length);
        var branches = new string[count];

        for (var index = 0; index < count; index++)
        {
            branches[index] = $"'{matchingSourceTypes[index]}' -> " +
                              $"'{matchingDestinationTypes[index]}'";
        }

        return $"Runtime source type '{actualSourceType}' matches multiple " +
               $"equally specific branches for mapping '{sourceType}' -> " +
               $"'{destinationType}': {string.Join(", ", branches)}.";
    }
}
