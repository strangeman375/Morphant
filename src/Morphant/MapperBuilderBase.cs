using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Morphant.Exceptions;

namespace Morphant;

/// <summary>
/// Provides settings shared by mapper and mapping builders.
/// </summary>
/// <typeparam name="TBuilder">The concrete builder type.</typeparam>
/// <remarks>
/// This is infrastructure for Morphant configuration builders. User code does
/// not derive from it directly.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[ExcludeFromCodeCoverage]
public abstract class MapperBuilderBase<TBuilder>
    where TBuilder : MapperBuilderBase<TBuilder>
{
    private protected MapperBuilderBase()
    {
    }

    /// <summary>
    /// Configures how mappings handle a <see langword="null"/> source.
    /// </summary>
    /// <param name="nullSourceHandling">
    /// The compile-time constant policy.
    /// <see cref="Morphant.NullSourceHandling.Default"/> inherits the setting;
    /// the fallback is
    /// <see cref="Morphant.NullSourceHandling.ReturnNull"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public TBuilder NullSourceHandling(
        NullSourceHandling nullSourceHandling) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures how mappings handle a <see langword="null"/> destination.
    /// </summary>
    /// <param name="nullDestinationHandling">
    /// The compile-time constant policy.
    /// <see cref="Morphant.NullDestinationHandling.Default"/> inherits the
    /// setting; the fallback is
    /// <see cref="Morphant.NullDestinationHandling.Create"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public TBuilder NullDestinationHandling(
        NullDestinationHandling nullDestinationHandling) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures how a polymorphic mapping handles a non-exact runtime
    /// source type that matches no derived branch.
    /// </summary>
    /// <param name="unknownDerivedTypeHandling">
    /// The compile-time constant policy.
    /// <see cref="Morphant.UnknownDerivedTypeHandling.Default"/> inherits the
    /// setting; the fallback is
    /// <see cref="Morphant.UnknownDerivedTypeHandling.UseBaseMapping"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public TBuilder UnknownDerivedTypeHandling(
        UnknownDerivedTypeHandling unknownDerivedTypeHandling) =>
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
    public TBuilder ConstructorSelection(
        ConstructorSelection constructorSelection) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures selection of destination members without explicit rules.
    /// </summary>
    /// <param name="memberSelection">
    /// The compile-time constant policy.
    /// <see cref="Morphant.MemberSelection.Default"/> inherits the setting;
    /// the fallback is <see cref="Morphant.MemberSelection.Auto"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public TBuilder MemberSelection(MemberSelection memberSelection) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures convention-based flattening of nested source members.
    /// </summary>
    /// <param name="flattening">
    /// The compile-time constant policy.
    /// <see cref="Morphant.Flattening.Default"/> inherits the setting; the
    /// fallback is <see cref="Morphant.Flattening.Auto"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public TBuilder Flattening(Flattening flattening) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures validation of unmapped source and destination members.
    /// </summary>
    /// <param name="unmappedMemberValidation">
    /// The compile-time constant policy.
    /// <see cref="Morphant.UnmappedMemberValidation.Default"/> inherits the
    /// setting; the fallback is
    /// <see cref="Morphant.UnmappedMemberValidation.None"/>.
    /// </param>
    /// <returns>This builder.</returns>
    public TBuilder UnmappedMemberValidation(
        UnmappedMemberValidation unmappedMemberValidation) =>
        throw new RuntimeInvocationNotSupportedException();
}
