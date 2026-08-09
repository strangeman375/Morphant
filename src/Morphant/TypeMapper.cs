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
/// mapping. Application code normally invokes it through
/// <see cref="IMapper"/> or the context-free
/// <see cref="TypeMapperExtensions.Create{TSource, TDestination}(ITypeMapper{TSource, TDestination}, TSource)"/>
/// and <see cref="TypeMapperExtensions.Update{TSource, TDestination}(ITypeMapper{TSource, TDestination}, TSource, TDestination)"/>
/// extensions, which create a valid root mapping scope.
/// </remarks>
public interface ITypeMapper<in TSource, TDestination>
{
    /// <summary>
    /// Maps the specified source without a supplied destination.
    /// </summary>
    /// <param name="source">
    /// The source to map. May be <see langword="null"/>. Declarative mappings
    /// apply the effective <see cref="NullSourceHandling"/> setting; a manual
    /// <c>Convert</c> receives the original value instead.
    /// </param>
    /// <param name="context">
    /// The context for the mapping operation. A default-initialized value is
    /// usable only when the selected mapping does not read context data.
    /// </param>
    /// <returns>The mapped destination.</returns>
    /// <exception cref="MappingConfigurationException">
    /// The effective configuration is invalid or the selected mapping plan
    /// cannot be represented in generated code.
    /// </exception>
    /// <exception cref="MappingOperationNotSupportedException">
    /// The effective <see cref="MappingMode"/> does not include
    /// <see cref="MappingMode.Create"/>.
    /// </exception>
    /// <exception cref="NullSourceException">
    /// A declarative mapping is selected, <paramref name="source"/> is
    /// <see langword="null"/>, and the effective
    /// <see cref="NullSourceHandling"/> is
    /// <see cref="Morphant.NullSourceHandling.Throw"/>.
    /// </exception>
    TDestination Create(TSource? source, MappingContext context);

    /// <summary>
    /// Maps the specified source with a supplied destination.
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
    /// <param name="context">
    /// The context for the mapping operation. A default-initialized value is
    /// usable only when the selected mapping does not read context data.
    /// </param>
    /// <returns>
    /// The authoritative mapped destination. It may be a replacement for
    /// <paramref name="destination"/>.
    /// </returns>
    /// <exception cref="MappingConfigurationException">
    /// The effective configuration is invalid or the selected mapping plan
    /// cannot be represented in generated code.
    /// </exception>
    /// <exception cref="MappingOperationNotSupportedException">
    /// The effective <see cref="MappingMode"/> does not include
    /// <see cref="MappingMode.Update"/>.
    /// </exception>
    /// <exception cref="NullSourceException">
    /// A declarative mapping is selected, <paramref name="source"/> is
    /// <see langword="null"/>, and the effective
    /// <see cref="NullSourceHandling"/> is
    /// <see cref="Morphant.NullSourceHandling.Throw"/>.
    /// </exception>
    /// <exception cref="NullDestinationException">
    /// A declarative mapping is selected,
    /// <paramref name="destination"/> is <see langword="null"/> and the
    /// effective <see cref="NullDestinationHandling"/> is
    /// <see cref="Morphant.NullDestinationHandling.Throw"/>.
    /// </exception>
    TDestination Update(
        TSource? source,
        TDestination? destination,
        MappingContext context);
}

public abstract class TypeMapper
{
    /// <summary>
    /// Determines whether this mapper declares the specified exact mapping
    /// pair.
    /// </summary>
    /// <param name="sourceType">The exact source type.</param>
    /// <param name="destinationType">The exact destination type.</param>
    /// <returns>
    /// <see langword="true"/> when the mapping pair is declared; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// This infrastructure member describes pair declarations independently
    /// of the operations enabled by their effective configuration.
    /// </remarks>
    protected internal virtual bool Supports(
        global::System.Type sourceType,
        global::System.Type destinationType) =>
        false;

    protected abstract void Configure(MapperBuilder builder);

    protected static ByConventionMarker ByConvention() =>
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
    /// Configures a declarative nested mapping whose source is inferred from
    /// the target name and whose operation follows the outer mapping.
    /// </summary>
    protected static MapMarker Map() =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures a declarative nested mapping whose operation follows the
    /// outer mapping.
    /// </summary>
    /// <param name="source">The source passed to the nested mapping.</param>
    protected static MapMarker Map(object? source) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures a declarative nested mapping to <typeparamref name="T"/>
    /// whose source is inferred from the target name and whose operation
    /// follows the outer mapping.
    /// </summary>
    /// <typeparam name="T">The nested destination type.</typeparam>
    protected static MapMarker<T> Map<T>() =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures a declarative nested mapping to <typeparamref name="T"/>
    /// whose operation follows the outer mapping.
    /// </summary>
    /// <typeparam name="T">The nested destination type.</typeparam>
    /// <param name="source">The source passed to the nested mapping.</param>
    protected static MapMarker<T> Map<T>(object? source) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures a declarative nested Create mapping whose destination type
    /// is inferred from the target member or constructor parameter.
    /// </summary>
    /// <param name="source">The source passed to the nested mapping.</param>
    protected static MapMarker Create(object? source) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures a declarative nested Create mapping to
    /// <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The nested destination type.</typeparam>
    /// <param name="source">The source passed to the nested mapping.</param>
    protected static MapMarker<T> Create<T>(object? source) =>
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
    protected static MapMarker Update(
        object? source,
        object? destination) =>
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
    protected static MapMarker<T> Update<T>(
        object? source,
        object? destination) =>
        throw new RuntimeInvocationNotSupportedException();
}
