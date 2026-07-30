using Morphant.Exceptions;

namespace Morphant;

public abstract class MapperBuilderBase<T>
    where T : MapperBuilderBase<T>
{
    private protected MapperBuilderBase()
    {
    }

    public T TemplateSurface(TemplateSurface templateSurface) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures how this builder handles a
    /// <see langword="null"/> source.
    /// </summary>
    /// <param name="nullSourceHandling">
    /// The behavior to apply. <see cref="Morphant.NullSourceHandling.Default"/>
    /// inherits the mapper-level setting for a mapping builder, or the
    /// assembly-level <c>MorphantNullSourceHandling</c> MSBuild property for
    /// the mapper builder. If all levels inherit, Morphant uses
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
    /// mapper-level setting for a mapping builder, or the assembly-level
    /// <c>MorphantNullDestinationHandling</c> MSBuild property for the mapper
    /// builder. If all levels inherit, Morphant uses
    /// <see cref="Morphant.NullDestinationHandling.CreateNew"/>.
    /// The argument expression must be a compile-time constant whose value is
    /// defined by <see cref="Morphant.NullDestinationHandling"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public T NullDestinationHandling(
        NullDestinationHandling nullDestinationHandling) =>
        throw new RuntimeInvocationNotSupportedException();

    public T ConstructorSelection(ConstructorSelection constructorSelection) =>
        throw new RuntimeInvocationNotSupportedException();

    public T MemberMatching(MemberMatching memberMatching) =>
        throw new RuntimeInvocationNotSupportedException();

    public T UnmappedMemberValidation(UnmappedMemberValidation unmappedMemberValidation) =>
        throw new RuntimeInvocationNotSupportedException();

    public T NullabilityMismatchValidation(NullabilityMismatchValidation nullabilityMismatchValidation) =>
        throw new RuntimeInvocationNotSupportedException();
}

public abstract class MapperBuilder : MapperBuilderBase<MapperBuilder>
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
    /// inherits the assembly-level <c>MorphantMappingMode</c> MSBuild
    /// property. If that property is not set or is also <c>Default</c>,
    /// Morphant uses <see cref="Morphant.MappingMode.MapNewAndExisting"/>.
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
    /// inherits the mapper-level setting, then the assembly-level
    /// <c>MorphantMappingMode</c> MSBuild property.
    /// The argument expression must be a compile-time constant composed only
    /// from the defined mapping mode flags.
    /// </param>
    /// <returns>The builder for the registered mapping.</returns>
    public MapperBuilder<TSource, TDestination> Map<TSource, TDestination>(MappingMode mappingMode = Morphant.MappingMode.Default) =>
        throw new RuntimeInvocationNotSupportedException();
}

public abstract class MapperBuilder<TSource, TDestination> : MapperBuilderBase<MapperBuilder<TSource, TDestination>>
{
    private MapperBuilder()
    {
    }
}
