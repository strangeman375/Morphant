using System.Diagnostics.CodeAnalysis;
using Morphant.Exceptions;

namespace Morphant;

/// <summary>
/// Base class for mapper configuration builders.
/// </summary>
/// <typeparam name="T">The concrete builder type.</typeparam>
/// <remarks>
/// Morphant interprets calls to this builder at compile time. Executing them
/// at runtime throws <see cref="RuntimeInvocationNotSupportedException"/>.
/// </remarks>
[ExcludeFromCodeCoverage]
public abstract class MapperBuilderBase<T>
    where T : MapperBuilderBase<T>
{
    private protected MapperBuilderBase()
    {
    }

    /// <summary>
    /// Configures how the mapping handles a <see langword="null"/> source.
    /// </summary>
    /// <param name="nullSourceHandling">
    /// The compile-time constant policy.
    /// <see cref="Morphant.NullSourceHandling.Default"/> inherits the setting;
    /// the fallback is
    /// <see cref="Morphant.NullSourceHandling.ReturnNull"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public T NullSourceHandling(
        NullSourceHandling nullSourceHandling) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures how the mapping handles a <see langword="null"/>
    /// destination.
    /// </summary>
    /// <param name="nullDestinationHandling">
    /// The compile-time constant policy.
    /// <see cref="Morphant.NullDestinationHandling.Default"/> inherits the
    /// setting; the fallback is
    /// <see cref="Morphant.NullDestinationHandling.Create"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public T NullDestinationHandling(
        NullDestinationHandling nullDestinationHandling) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures constructor selection for convention-based creation.
    /// </summary>
    /// <param name="constructorSelection">
    /// The compile-time constant policy.
    /// <see cref="Morphant.ConstructorSelection.Default"/> inherits the
    /// setting; the fallback is
    /// <see cref="Morphant.ConstructorSelection.Unambiguous"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public T ConstructorSelection(
        ConstructorSelection constructorSelection) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures selection of destination members without explicit rules.
    /// </summary>
    /// <param name="memberSelection">
    /// The compile-time constant policy.
    /// <see cref="Morphant.MemberSelection.Default"/> inherits the
    /// setting; the fallback is <see cref="Morphant.MemberSelection.Auto"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public T MemberSelection(MemberSelection memberSelection) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures validation of members omitted from the mapping plan.
    /// </summary>
    /// <param name="unmappedMemberValidation">
    /// The compile-time constant policy.
    /// <see cref="Morphant.UnmappedMemberValidation.Default"/> inherits the
    /// setting; the fallback is
    /// <see cref="Morphant.UnmappedMemberValidation.None"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public T UnmappedMemberValidation(
        UnmappedMemberValidation unmappedMemberValidation) =>
        throw new RuntimeInvocationNotSupportedException();
}

/// <summary>
/// Configures mappings declared by a mapper.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class MapperBuilder : MapperBuilderBase<MapperBuilder>
{
    private MapperBuilder()
    {
    }

    /// <summary>
    /// Configures the default operations for this mapper.
    /// </summary>
    /// <param name="mappingMode">
    /// The compile-time constant operations to generate.
    /// <see cref="Morphant.MappingMode.Default"/> inherits the setting; the
    /// fallback is
    /// <see cref="Morphant.MappingMode.CreateAndUpdate"/>.
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
    /// The compile-time constant operations to generate.
    /// <see cref="Morphant.MappingMode.Default"/> continues through normal
    /// setting precedence; the fallback is
    /// <see cref="Morphant.MappingMode.CreateAndUpdate"/>.
    /// </param>
    /// <returns>The builder for the registered mapping.</returns>
    public MapperBuilder<TSource, TDestination> Map<TSource, TDestination>(MappingMode mappingMode = Morphant.MappingMode.Default) =>
        throw new RuntimeInvocationNotSupportedException();
}

/// <summary>
/// Configures mapping from <typeparamref name="TSource"/> to
/// <typeparamref name="TDestination"/>.
/// </summary>
/// <typeparam name="TSource">The source type.</typeparam>
/// <typeparam name="TDestination">The destination type.</typeparam>
[ExcludeFromCodeCoverage]
public sealed class MapperBuilder<TSource, TDestination> : MapperBuilderBase<MapperBuilder<TSource, TDestination>>
{
    private MapperBuilder()
    {
    }

    /// <summary>
    /// Includes configuration from the nearest available mapping for the
    /// specified source and destination types.
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
    /// Local settings and rules take precedence. A mapping declared in a base
    /// mapper is available only through <c>base.Configure(builder)</c>. A
    /// different pair contributes settings and explicit member rules, but not
    /// its result or <c>Convert</c> plan; the exact same pair contributes its
    /// full plan.
    /// </remarks>
    /// <returns>This mapping builder.</returns>
    public MapperBuilder<TSource, TDestination>
        IncludeBase<TBaseSource, TBaseDestination>() =>
        throw new RuntimeInvocationNotSupportedException();
}
