using Morphant.Context;
using Morphant.Exceptions;
using Morphant.Markers;

namespace Morphant;

/// <summary>
/// Represents mapping operations from <typeparamref name="TSource"/> to
/// <typeparamref name="TDestination"/>.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
/// <remarks>
/// Morphant generates an implementation of this interface for each configured
/// mapping.
/// </remarks>
public interface ITypeMapper<in TSource, TDestination>
{
    /// <summary>
    /// Maps the specified source to a new destination.
    /// </summary>
    /// <param name="source">
    /// The source to map. May be <see langword="null"/>. Declarative mappings
    /// apply the effective <see cref="NullSourceHandling"/> setting; a manual
    /// <c>Convert</c> receives the original value instead.
    /// </param>
    /// <param name="context">The context for the mapping operation.</param>
    /// <returns>The mapped destination.</returns>
    /// <exception cref="NotSupportedException">
    /// The effective <see cref="MappingMode"/> is invalid or does not include
    /// <see cref="MappingMode.Create"/>, or a null-handling setting required
    /// by the selected declarative mapping is invalid.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// A declarative mapping is selected, <paramref name="source"/> is
    /// <see langword="null"/>, and the effective
    /// <see cref="NullSourceHandling"/> is
    /// <see cref="Morphant.NullSourceHandling.Throw"/>.
    /// </exception>
    TDestination Map(TSource? source, MappingContext context);

    /// <summary>
    /// Maps the specified source to the specified destination.
    /// </summary>
    /// <param name="source">
    /// The source to map. May be <see langword="null"/>. Declarative mappings
    /// apply the effective <see cref="NullSourceHandling"/> setting; a manual
    /// <c>Convert</c> receives the original value instead.
    /// </param>
    /// <param name="destination">
    /// The destination to map to. May be <see langword="null"/>. Declarative
    /// mappings apply the effective <see cref="NullDestinationHandling"/>
    /// setting; a manual <c>Convert</c> receives
    /// <see cref="Option{TDestination}.None"/> instead.
    /// </param>
    /// <param name="context">The context for the mapping operation.</param>
    /// <returns>The mapped destination.</returns>
    /// <exception cref="NotSupportedException">
    /// The effective <see cref="MappingMode"/> is invalid or does not include
    /// <see cref="MappingMode.Update"/>, or a null-handling setting required
    /// by the selected declarative mapping is invalid.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// A declarative mapping is selected, <paramref name="source"/> is
    /// <see langword="null"/>, and the effective
    /// <see cref="NullSourceHandling"/> is
    /// <see cref="Morphant.NullSourceHandling.Throw"/>, or
    /// <paramref name="destination"/> is <see langword="null"/> and the
    /// effective <see cref="NullDestinationHandling"/> is
    /// <see cref="Morphant.NullDestinationHandling.Throw"/>.
    /// </exception>
    TDestination Map(
        TSource? source,
        TDestination? destination,
        MappingContext context);
}

public abstract class TypeMapper
{
    protected abstract void Configure(MapperBuilder builder);

    protected static ByConventionMarker ByConvention() =>
        throw new RuntimeInvocationNotSupportedException();

    protected static IByFactoryMarker<TDestination> ByFactory<TDestination>(Func<TDestination> factory) =>
        throw new RuntimeInvocationNotSupportedException();

    protected static AutoMarker Auto() =>
        throw new RuntimeInvocationNotSupportedException();

    protected static AutoMarker<T> Auto<T>() =>
        throw new RuntimeInvocationNotSupportedException();

    protected static IgnoreMarker Ignore() =>
        throw new RuntimeInvocationNotSupportedException();

    protected static IgnoreMarker<T> Ignore<T>() =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures a declarative nested Create mapping whose destination type
    /// is inferred from the target member or constructor parameter.
    /// </summary>
    /// <param name="source">The source passed to the nested mapping.</param>
    protected static MapMarker Map(object? source) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures a declarative nested Update mapping whose destination type
    /// is inferred from the target member or constructor parameter.
    /// </summary>
    /// <param name="source">The source passed to the nested mapping.</param>
    /// <param name="destination">
    /// The destination passed to the nested mapping, including an explicit
    /// <see langword="null"/> destination.
    /// </param>
    protected static MapMarker Map(object? source, object? destination) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures a declarative nested Create mapping to
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The nested destination type.</typeparam>
    /// <param name="source">The source passed to the nested mapping.</param>
    protected static MapMarker<T> Map<T>(object? source) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures a declarative nested Update mapping to
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The nested destination type.</typeparam>
    /// <param name="source">The source passed to the nested mapping.</param>
    /// <param name="destination">
    /// The destination passed to the nested mapping, including an explicit
    /// <see langword="null"/> destination.
    /// </param>
    protected static MapMarker<T> Map<T>(object? source, object? destination) =>
        throw new RuntimeInvocationNotSupportedException();
}
