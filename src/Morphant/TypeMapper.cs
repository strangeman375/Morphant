using System.Diagnostics.CodeAnalysis;
using Morphant.Context;
using Morphant.Exceptions;
using Morphant.Markers;

namespace Morphant;

/// <summary>
/// Maps <typeparamref name="TSource"/> to
/// <typeparamref name="TDestination"/>.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TDestination">
/// The destination type. Use a nullable reference type when the mapping can
/// return null.
/// </typeparam>
/// <remarks>
/// Morphant generates one implementation per configured mapping. Prefer
/// <see cref="IMapper"/> or the context-free extension methods for direct use.
/// </remarks>
public interface ITypeMapper<in TSource, TDestination>
{
    /// <summary>
    /// Maps the specified source without a supplied destination.
    /// </summary>
    /// <param name="source">
    /// The source to map, which may be <see langword="null"/>.
    /// </param>
    /// <param name="context">
    /// The current mapping context.
    /// </param>
    /// <returns>
    /// The mapping result, which may be <see langword="default"/> when allowed
    /// by the mapping.
    /// </returns>
    /// <exception cref="MappingConfigurationException">
    /// The mapping configuration is invalid.
    /// </exception>
    /// <exception cref="MappingOperationNotSupportedException">
    /// The mapping does not support <see cref="MappingMode.Create"/>.
    /// </exception>
    /// <exception cref="NullSourceException">
    /// The null-source policy rejects <paramref name="source"/>.
    /// </exception>
    TDestination Create(TSource? source, MappingContext context);

    /// <summary>
    /// Maps the specified source with a supplied destination.
    /// </summary>
    /// <param name="source">
    /// The source to map, which may be <see langword="null"/>.
    /// </param>
    /// <param name="destination">
    /// The existing destination, which may be <see langword="null"/>.
    /// </param>
    /// <param name="context">
    /// The current mapping context.
    /// </param>
    /// <returns>
    /// The mapping result. It may replace <paramref name="destination"/> or be
    /// <see langword="default"/> when allowed by the mapping.
    /// </returns>
    /// <exception cref="MappingConfigurationException">
    /// The mapping configuration is invalid.
    /// </exception>
    /// <exception cref="MappingOperationNotSupportedException">
    /// The mapping does not support <see cref="MappingMode.Update"/>.
    /// </exception>
    /// <exception cref="NullSourceException">
    /// The null-source policy rejects <paramref name="source"/>.
    /// </exception>
    /// <exception cref="NullDestinationException">
    /// The null-destination policy rejects <paramref name="destination"/>.
    /// </exception>
    TDestination Update(
        TSource? source,
        TDestination? destination,
        MappingContext context);
}

/// <summary>
/// Runtime metadata implemented by generated mapper declarations.
/// </summary>
/// <remarks>
/// This contract supports Morphant runtime dispatch and is not a
/// configuration entry point.
/// </remarks>
[global::System.ComponentModel.EditorBrowsable(
    global::System.ComponentModel.EditorBrowsableState.Never)]
public interface IMapperDeclaration
{
    /// <summary>
    /// Checks whether the mapper declares an exact mapping pair.
    /// </summary>
    bool Supports(
        global::System.Type sourceType,
        global::System.Type destinationType);
}

/// <summary>
/// Base class for compile-time mapper declarations.
/// </summary>
/// <typeparam name="TMapper">
/// The concrete mapper at the end of the configuration hierarchy.
/// </typeparam>
[ExcludeFromCodeCoverage]
public abstract class TypeMapper<TMapper> : IMapperDeclaration
    where TMapper : TypeMapper<TMapper>
{
    /// <summary>
    /// Checks whether this mapper declares an exact mapping pair.
    /// </summary>
    /// <param name="sourceType">The exact source type.</param>
    /// <param name="destinationType">The exact destination type.</param>
    /// <returns>Whether the pair is declared.</returns>
    /// <remarks>
    /// Morphant generates this override. Mapper declarations must not
    /// override it manually.
    /// </remarks>
    protected internal virtual bool Supports(
        global::System.Type sourceType,
        global::System.Type destinationType) =>
        false;

    bool IMapperDeclaration.Supports(
        global::System.Type sourceType,
        global::System.Type destinationType) =>
        Supports(sourceType, destinationType);

    /// <summary>
    /// Declares mappings for this mapper.
    /// </summary>
    /// <param name="builder">The mapper builder.</param>
    /// <remarks>
    /// Morphant analyzes this method at compile time and does not invoke it at
    /// runtime.
    /// </remarks>
    protected abstract void Configure(MapperBuilder builder);

    /// <summary>
    /// Selects convention-based construction.
    /// </summary>
    protected static ByConventionMarker ByConvention() =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Selects convention-based mapping for the current target.
    /// </summary>
    protected static AutoMarker Auto() =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Selects convention-based mapping to <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    protected static AutoMarker<T> Auto<T>() =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Skips the current member or constructor argument.
    /// </summary>
    protected static IgnoreMarker Ignore() =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Skips a target of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    protected static IgnoreMarker<T> Ignore<T>() =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Uses an explicit value for the current target.
    /// </summary>
    /// <typeparam name="T">The target value type.</typeparam>
    /// <param name="value">The value expression.</param>
    /// <returns>The value marker.</returns>
    /// <remarks>
    /// Use only inside <c>Construct</c>, <c>Resolve</c>, or <c>Members</c>.
    /// </remarks>
    protected static ValueMarker<T> Value<T>(T value) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Maps a value inferred by name, selecting nested Create or Update from
    /// the outer operation and current nested value.
    /// </summary>
    protected static MapMarker Map() =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Maps a supplied value, selecting nested Create or Update from the outer
    /// operation and current nested value.
    /// </summary>
    /// <param name="source">The source passed to the nested mapping.</param>
    protected static MapMarker Map(object? source) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Maps a value inferred by name to <typeparamref name="T"/>, selecting
    /// nested Create or Update from the outer operation and current nested
    /// value.
    /// </summary>
    /// <typeparam name="T">The nested destination type.</typeparam>
    protected static MapMarker<T> Map<T>() =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Maps a supplied value to <typeparamref name="T"/>, selecting nested
    /// Create or Update from the outer operation and current nested value.
    /// </summary>
    /// <typeparam name="T">The nested destination type.</typeparam>
    /// <param name="source">The source passed to the nested mapping.</param>
    protected static MapMarker<T> Map<T>(object? source) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Creates a nested destination whose type is inferred from the target.
    /// </summary>
    /// <param name="source">The source passed to the nested mapping.</param>
    protected static MapMarker Create(object? source) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Creates a nested destination of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The nested destination type.</typeparam>
    /// <param name="source">The source passed to the nested mapping.</param>
    protected static MapMarker<T> Create<T>(object? source) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Updates a nested destination whose type is inferred from the target.
    /// </summary>
    /// <param name="source">The source passed to the nested mapping.</param>
    /// <param name="destination">
    /// The existing destination, which may be <see langword="null"/>.
    /// </param>
    protected static MapMarker Update(
        object? source,
        object? destination) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Updates a nested destination of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The nested destination type.</typeparam>
    /// <param name="source">The source passed to the nested mapping.</param>
    /// <param name="destination">
    /// The existing destination, which may be <see langword="null"/>.
    /// </param>
    protected static MapMarker<T> Update<T>(
        object? source,
        object? destination) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures mappings declared by this mapper.
    /// </summary>
    [ExcludeFromCodeCoverage]
    protected sealed class MapperBuilder : MapperBuilderBase<MapperBuilder>
    {
        private MapperBuilder()
        {
        }

        /// <summary>
        /// Configures the default operations for this mapper.
        /// </summary>
        /// <param name="mappingMode">
        /// The compile-time constant operations to generate.
        /// <see cref="Morphant.MappingMode.Default"/> inherits the setting;
        /// the fallback is
        /// <see cref="Morphant.MappingMode.CreateAndUpdate"/>.
        /// </param>
        /// <returns>This builder.</returns>
        public MapperBuilder MappingMode(MappingMode mappingMode) =>
            throw new RuntimeInvocationNotSupportedException();

        /// <summary>
        /// Registers a mapping from <typeparamref name="TSource"/> to
        /// <typeparamref name="TDestination"/> in this mapper scope.
        /// </summary>
        /// <typeparam name="TSource">The source type.</typeparam>
        /// <typeparam name="TDestination">The destination type.</typeparam>
        /// <param name="mappingMode">
        /// The compile-time constant operations to generate.
        /// <see cref="Morphant.MappingMode.Default"/> continues through
        /// normal setting precedence; the fallback is
        /// <see cref="Morphant.MappingMode.CreateAndUpdate"/>.
        /// </param>
        /// <returns>The builder for the registered mapping.</returns>
        public global::Morphant.MappingBuilder<
            TMapper,
            TSource,
            TDestination> Map<TSource, TDestination>(
                MappingMode mappingMode =
                    Morphant.MappingMode.Default) =>
            throw new RuntimeInvocationNotSupportedException();
    }
}
