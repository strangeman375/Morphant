using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ConventionSourceMemberResolver
{
    public static ConventionSourceMemberResolution ResolveExact(
        ConventionSourceMemberContext context,
        string targetName,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var direct = context.DirectMembers.FirstOrDefault(member =>
            StringComparer.Ordinal.Equals(member.Name, targetName));

        if (direct.Symbol is not null)
        {
            return ConventionSourceMemberResolution.Direct(
                ImmutableArray.Create(direct));
        }

        return ResolveFlattened(
            context,
            targetName,
            StringComparison.Ordinal,
            compilation,
            mapperType,
            cancellationToken);
    }

    public static ConventionSourceMemberResolution ResolveConstructor(
        ConventionSourceMemberContext context,
        string targetName,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var exact = context.DirectMembers.Where(member =>
                StringComparer.Ordinal.Equals(member.Name, targetName))
            .ToImmutableArray();

        if (!exact.IsEmpty)
        {
            return ConventionSourceMemberResolution.Direct(exact);
        }

        var insensitive = context.DirectMembers.Where(member =>
                StringComparer.OrdinalIgnoreCase.Equals(
                    member.Name,
                    targetName))
            .ToImmutableArray();

        if (!insensitive.IsEmpty)
        {
            return ConventionSourceMemberResolution.Direct(insensitive);
        }

        return ResolveFlattened(
            context,
            targetName,
            StringComparison.Ordinal,
            compilation,
            mapperType,
            cancellationToken);
    }

    public static ConventionSourceMemberResolution
        ResolveConstructorCaseInsensitiveFlattened(
            ConventionSourceMemberContext context,
            string targetName,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            CancellationToken cancellationToken) =>
        ResolveFlattened(
            context,
            targetName,
            StringComparison.OrdinalIgnoreCase,
            compilation,
            mapperType,
            cancellationToken);

    private static ConventionSourceMemberResolution ResolveFlattened(
        ConventionSourceMemberContext context,
        string targetName,
        StringComparison comparison,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (context.Flattening != FlatteningValue.Auto ||
            targetName.Length == 0)
        {
            return ConventionSourceMemberResolution.None;
        }

        var rootScopeIndex = context.IncludedScopes.IsEmpty
            ? 0
            : context.IncludedScopes.Max(static scope =>
                scope.Access.ScopeIndex) + 1;
        var rootAccess = new ConventionSourceAccessModel(
            rootScopeIndex,
            context.RootType,
            RequiresRootCast: false,
            ImmutableArray<ConventionSourcePathSegment>.Empty);
        var rootCandidates = FindFlattenedCandidates(
            context.RootType,
            targetName,
            targetName,
            comparison,
            rootAccess,
            depth: 0,
            compilation,
            mapperType,
            cancellationToken);

        var includedCandidates =
            ImmutableArray.CreateBuilder<ConventionReadableMember>();

        foreach (var scope in context.IncludedScopes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            includedCandidates.AddRange(FindFlattenedCandidates(
                scope.SelectedType,
                targetName,
                targetName,
                comparison,
                scope.Access,
                depth: 0,
                compilation,
                mapperType,
                cancellationToken));
        }

        var orderedRoot = OrderAndDeduplicate(rootCandidates);
        var orderedIncluded = OrderAndDeduplicate(
            includedCandidates.ToImmutable());

        return orderedRoot.IsEmpty && orderedIncluded.IsEmpty
            ? ConventionSourceMemberResolution.None
            : ConventionSourceMemberResolution.Flattened(
                orderedRoot.IsEmpty ? orderedIncluded : orderedRoot,
                orderedRoot.IsEmpty
                    ? ImmutableArray<ConventionReadableMember>.Empty
                    : orderedIncluded);
    }

    private static ImmutableArray<ConventionReadableMember>
        FindFlattenedCandidates(
            ITypeSymbol receiverType,
            string remainingName,
            string targetName,
            StringComparison comparison,
            ConventionSourceAccessModel access,
            int depth,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            CancellationToken cancellationToken)
    {
        var result =
            ImmutableArray.CreateBuilder<ConventionReadableMember>();
        var readableMembers = ConventionMemberMappingPlanner
            .BuildReadableMembers(
                ConventionSourceAccessModel.NormalizeReceiverType(
                    receiverType),
                compilation,
                mapperType,
                cancellationToken);

        foreach (var member in readableMembers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (member.Name.Length > remainingName.Length ||
                !remainingName.StartsWith(member.Name, comparison))
            {
                continue;
            }

            if (member.Name.Length == remainingName.Length)
            {
                if (depth > 0)
                {
                    result.Add(BuildCandidate(
                        targetName,
                        member,
                        access,
                        compilation));
                }

                continue;
            }

            result.AddRange(FindFlattenedCandidates(
                member.Type,
                remainingName.Substring(member.Name.Length),
                targetName,
                comparison,
                access.Append(member),
                depth + 1,
                compilation,
                mapperType,
                cancellationToken));
        }

        return result.ToImmutable();
    }

    private static ConventionReadableMember BuildCandidate(
        string targetName,
        ConventionReadableMember terminal,
        ConventionSourceAccessModel access,
        CSharpCompilation compilation)
    {
        var pathMembers = access.Path
            .Select(static segment => segment.Symbol)
            .Append(terminal.Symbol)
            .ToImmutableArray();
        var pathDisplay = string.Join(
            ".",
            access.Path.Select(static segment => segment.Name)
                .Append(terminal.Name));
        var pathIdentity = string.Join(
            "/",
            pathMembers.Select(
                ConventionSourceAccessModel.MemberIdentity));

        return new ConventionReadableMember(
            targetName,
            access.CanProduceMissingValue
                ? ConventionSourceAccessModel.LiftMissingValueType(
                    terminal.Type,
                    compilation)
                : terminal.Type,
            terminal.Symbol,
            access,
            pathDisplay,
            pathIdentity,
            pathMembers);
    }

    private static ImmutableArray<ConventionReadableMember>
        OrderAndDeduplicate(
            ImmutableArray<ConventionReadableMember> candidates)
    {
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var result =
            ImmutableArray.CreateBuilder<ConventionReadableMember>();

        foreach (var candidate in candidates.OrderBy(
                     static candidate => candidate.PathDisplay,
                     StringComparer.Ordinal))
        {
            if (identities.Add(candidate.PathIdentity!))
            {
                result.Add(candidate);
            }
        }

        return result.ToImmutable();
    }
}

internal readonly record struct ConventionSourceMemberContext(
    ITypeSymbol RootType,
    ImmutableArray<ConventionReadableMember> DirectMembers,
    ImmutableArray<IncludedSourceScope> IncludedScopes,
    FlatteningValue Flattening);

internal readonly record struct ConventionSourceMemberResolution(
    bool HasDirectClaim,
    ImmutableArray<ConventionReadableMember> Candidates,
    ImmutableArray<ConventionReadableMember> FallbackCandidates)
{
    public static ConventionSourceMemberResolution None =>
        new(
            false,
            ImmutableArray<ConventionReadableMember>.Empty,
            ImmutableArray<ConventionReadableMember>.Empty);

    public static ConventionSourceMemberResolution Direct(
        ImmutableArray<ConventionReadableMember> candidates) =>
        new(
            true,
            candidates,
            ImmutableArray<ConventionReadableMember>.Empty);

    public static ConventionSourceMemberResolution Flattened(
        ImmutableArray<ConventionReadableMember> candidates,
        ImmutableArray<ConventionReadableMember> fallbackCandidates) =>
        new(false, candidates, fallbackCandidates);
}
