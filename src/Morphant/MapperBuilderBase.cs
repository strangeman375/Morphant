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
    /// the fallback is <see cref="Morphant.NullSourceHandling.ReturnNull"/>.
    /// </param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/settings/null-source-handling.md"/>
    public TBuilder NullSourceHandling(
        NullSourceHandling nullSourceHandling) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures how Update handles a <see langword="null"/> destination.
    /// </summary>
    /// <param name="nullDestinationHandling">
    /// The compile-time constant policy.
    /// <see cref="Morphant.NullDestinationHandling.Default"/> inherits the
    /// setting; the fallback is <see cref="Morphant.NullDestinationHandling.Create"/>.
    /// </param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/settings/null-destination-handling.md"/>
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
    /// setting; the fallback is <see cref="Morphant.UnknownDerivedTypeHandling.UseBaseMapping"/>.
    /// </param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/settings/unknown-derived-type-handling.md"/>
    public TBuilder UnknownDerivedTypeHandling(
        UnknownDerivedTypeHandling unknownDerivedTypeHandling) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures constructor selection for convention-based creation.
    /// </summary>
    /// <param name="constructorSelection">
    /// The compile-time constant policy.
    /// <see cref="Morphant.ConstructorSelection.Default"/> inherits the
    /// setting; the fallback is <see cref="Morphant.ConstructorSelection.Unambiguous"/>.
    /// </param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/settings/constructor-selection.md"/>
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
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/settings/member-selection.md"/>
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
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/settings/flattening.md"/>
    public TBuilder Flattening(Flattening flattening) =>
        throw new RuntimeInvocationNotSupportedException();

    /// <summary>
    /// Configures validation of unmapped source and destination members.
    /// </summary>
    /// <param name="unmappedMemberValidation">
    /// The compile-time constant policy.
    /// <see cref="Morphant.UnmappedMemberValidation.Default"/> inherits the
    /// setting; the fallback is <see cref="Morphant.UnmappedMemberValidation.None"/>.
    /// </param>
    /// <seealso href="https://github.com/strangeman375/Morphant/blob/main/docs/settings/unmapped-member-validation.md"/>
    public TBuilder UnmappedMemberValidation(
        UnmappedMemberValidation unmappedMemberValidation) =>
        throw new RuntimeInvocationNotSupportedException();
}
