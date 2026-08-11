using Morphant.Exceptions;

namespace Morphant;

public abstract class MapperBuilderBase<T>
    where T : MapperBuilderBase<T>
{
    private protected MapperBuilderBase()
    {
    }

    /// <summary>
    /// Configures how this builder handles a
    /// <see langword="null"/> source.
    /// </summary>
    /// <param name="nullSourceHandling">
    /// The behavior to apply. <see cref="Morphant.NullSourceHandling.Default"/>
    /// continues through the included base pair, the current mapper root,
    /// connected base mapper roots, and the assembly-level
    /// <c>MorphantNullSourceHandling</c> MSBuild property. Levels that do not
    /// apply to this builder are skipped. If all levels inherit, Morphant uses
    /// <see cref="Morphant.NullSourceHandling.ReturnNull"/>.
    /// The argument expression must be a compile-time constant whose value is
    /// defined by <see cref="Morphant.NullSourceHandling"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public T NullSourceHandling(
        NullSourceHandling nullSourceHandling) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures how this builder handles a
    /// <see langword="null"/> existing destination.
    /// </summary>
    /// <param name="nullDestinationHandling">
    /// The behavior to apply.
    /// <see cref="Morphant.NullDestinationHandling.Default"/> inherits the
    /// included base pair, current mapper root, connected base mapper roots,
    /// and assembly-level <c>MorphantNullDestinationHandling</c> MSBuild
    /// property. Levels that do not apply to this builder are skipped. If all
    /// levels inherit, Morphant uses
    /// <see cref="Morphant.NullDestinationHandling.Create"/>.
    /// The argument expression must be a compile-time constant whose value is
    /// defined by <see cref="Morphant.NullDestinationHandling"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public T NullDestinationHandling(
        NullDestinationHandling nullDestinationHandling) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures how this builder selects a constructor for
    /// convention-based destination creation.
    /// </summary>
    /// <param name="constructorSelection">
    /// The selection policy to apply.
    /// <see cref="Morphant.ConstructorSelection.Default"/> inherits the
    /// included base pair, current mapper root, connected base mapper roots,
    /// and assembly-level <c>MorphantConstructorSelection</c> MSBuild
    /// property. Levels that do not apply to this builder are skipped. If all
    /// levels inherit, Morphant uses
    /// <see cref="Morphant.ConstructorSelection.Unambiguous"/>.
    /// The argument expression must be a compile-time constant whose value is
    /// defined by <see cref="Morphant.ConstructorSelection"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public T ConstructorSelection(
        ConstructorSelection constructorSelection) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures how destination members without an explicit
    /// <c>Members</c> rule are selected.
    /// </summary>
    /// <param name="memberSelection">
    /// The selection policy to apply.
    /// <see cref="Morphant.MemberSelection.Default"/> inherits the
    /// included base pair, current mapper root, connected base mapper roots,
    /// and assembly-level <c>MorphantMemberSelection</c> MSBuild property.
    /// Levels that do not apply to this builder are skipped. If all levels
    /// inherit, Morphant uses
    /// <see cref="Morphant.MemberSelection.Auto"/>.
    /// The argument expression must be a compile-time constant whose value is
    /// defined by <see cref="Morphant.MemberSelection"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public T MemberSelection(MemberSelection memberSelection) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures validation of source and destination members not used by
    /// the effective mapping plan.
    /// </summary>
    /// <param name="unmappedMemberValidation">
    /// The validation policy to apply.
    /// <see cref="Morphant.UnmappedMemberValidation.Default"/> inherits the
    /// included base pair, current mapper root, connected base mapper roots,
    /// and assembly-level <c>MorphantUnmappedMemberValidation</c> MSBuild
    /// property. Levels that do not apply to this builder are skipped. If all
    /// levels inherit, Morphant uses
    /// <see cref="Morphant.UnmappedMemberValidation.None"/>.
    /// The argument expression must be a compile-time constant whose value is
    /// defined by <see cref="Morphant.UnmappedMemberValidation"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public T UnmappedMemberValidation(
        UnmappedMemberValidation unmappedMemberValidation) =>
        throw new RuntimeInvocationNotSupportedException();
}

public sealed class MapperBuilder : MapperBuilderBase<MapperBuilder>
{
    private MapperBuilder()
    {
    }

    /// <summary>
    /// Configures the default mapping mode for mappings registered by this
    /// mapper.
    /// </summary>
    /// <param name="mappingMode">
    /// The mapping operations to support. <see cref="Morphant.MappingMode.Default"/>
    /// inherits connected base mapper roots, then the assembly-level
    /// <c>MorphantMappingMode</c> MSBuild property. If every level inherits,
    /// Morphant uses <see cref="Morphant.MappingMode.CreateAndUpdate"/>.
    /// The argument expression must be a compile-time constant composed only
    /// from the defined mapping mode flags.
    /// </param>
    /// <returns>This builder.</returns>
    public MapperBuilder MappingMode(MappingMode mappingMode) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Registers a mapping from <typeparamref name="TSource"/> to
    /// <typeparamref name="TDestination"/>.
    /// </summary>
    /// <typeparam name="TSource">The source type.</typeparam>
    /// <typeparam name="TDestination">The destination type.</typeparam>
    /// <param name="mappingMode">
    /// The mapping operations to support. <see cref="Morphant.MappingMode.Default"/>
    /// inherits an included base pair, the current mapper root, connected
    /// base mapper roots, and then the assembly-level
    /// <c>MorphantMappingMode</c> MSBuild property.
    /// The argument expression must be a compile-time constant composed only
    /// from the defined mapping mode flags.
    /// </param>
    /// <returns>The builder for the registered mapping.</returns>
    public MapperBuilder<TSource, TDestination> Map<TSource, TDestination>(MappingMode mappingMode = Morphant.MappingMode.Default) =>
        throw new RuntimeInvocationNotSupportedException();
}

public sealed class MapperBuilder<TSource, TDestination> : MapperBuilderBase<MapperBuilder<TSource, TDestination>>
{
    private MapperBuilder()
    {
    }

    /// <summary>
    /// Includes configuration from the nearest mapping of the specified base
    /// source and destination types on the current mapper level or in the
    /// connected base mapper configuration chain.
    /// </summary>
    /// <typeparam name="TBaseSource">
    /// The base source type. <typeparamref name="TSource"/> must be assignable
    /// to this type.
    /// </typeparam>
    /// <typeparam name="TBaseDestination">
    /// The base destination type. <typeparamref name="TDestination"/> must be
    /// assignable to this type.
    /// </typeparam>
    /// <remarks>
    /// A mapping on the current mapper level is preferred and may be declared
    /// before or after this mapping. An inherited mapping is available only
    /// when the mapper connects its configuration with an explicit
    /// <c>base.Configure(builder)</c> call. Morphant validates both type
    /// relationships during generation. An included different pair
    /// contributes all of its map-level settings and explicit
    /// <c>Members</c> rules. Conventions are evaluated again for the current
    /// source and destination types, and local rules override included rules
    /// for the same destination member. Its result policy and <c>Convert</c>
    /// plan are not included. An exact same pair from a connected base mapper
    /// contributes its complete applicable plan, including a result policy or
    /// <c>Convert</c>, under the documented local precedence rules.
    /// </remarks>
    /// <returns>This mapping builder.</returns>
    public MapperBuilder<TSource, TDestination>
        IncludeBase<TBaseSource, TBaseDestination>() =>
        throw new RuntimeInvocationNotSupportedException();
}
