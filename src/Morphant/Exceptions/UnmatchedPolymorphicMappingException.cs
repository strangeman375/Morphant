using Morphant.Context;

namespace Morphant.Exceptions;

/// <summary>
/// Thrown when an unknown derived runtime source is rejected.
/// </summary>
public sealed class UnmatchedPolymorphicMappingException : MappingException
{
    /// <summary>
    /// Creates the exception for a generated base mapping.
    /// </summary>
    /// <typeparam name="TSource">The requested base source type.</typeparam>
    /// <typeparam name="TDestination">The requested base destination
    /// type.</typeparam>
    /// <param name="operation">The requested operation.</param>
    /// <param name="source">The unmatched runtime source.</param>
    /// <returns>The initialized exception.</returns>
    public static UnmatchedPolymorphicMappingException
        Create<TSource, TDestination>(
            MappingOperation operation,
            object source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        return new UnmatchedPolymorphicMappingException(
            operation,
            typeof(TSource),
            typeof(TDestination),
            source.GetType());
    }

    /// <summary>
    /// Initializes the exception for the requested base mapping.
    /// </summary>
    /// <param name="operation">The requested operation.</param>
    /// <param name="sourceType">The requested base source type.</param>
    /// <param name="destinationType">The requested base destination type.</param>
    /// <param name="actualSourceType">The actual runtime source type.</param>
    public UnmatchedPolymorphicMappingException(
        MappingOperation operation,
        Type sourceType,
        Type destinationType,
        Type actualSourceType)
        : base(
            $"No polymorphic branch matches runtime source type " +
            $"'{actualSourceType}' for mapping '{sourceType}' -> " +
            $"'{destinationType}', and " +
            $"UnknownDerivedTypeHandling.Throw rejects base fallback.",
            operation,
            sourceType,
            destinationType)
    {
        ActualSourceType = actualSourceType ??
            throw new ArgumentNullException(nameof(actualSourceType));
    }

    /// <summary>
    /// Gets the unmatched runtime source type.
    /// </summary>
    public Type ActualSourceType { get; }
}
