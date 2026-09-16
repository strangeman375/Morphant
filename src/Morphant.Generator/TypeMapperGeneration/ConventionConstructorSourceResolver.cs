using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ConventionConstructorSourceResolver
{
    public static ImmutableArray<ResolvedConstructorSource> Resolve(
        ConventionSourceMemberContext context,
        ImmutableArray<IParameterSymbol> parameters,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken,
        SyntaxNode? originNode = null)
    {
        var resolutions = parameters.Select(parameter =>
            ConventionSourceMemberResolver.ResolveConstructor(
                context, parameter.Name, compilation, mapperType, cancellationToken)).ToImmutableArray();
        var compatible = FindCompatible(resolutions).ToArray();
        var insensitive = resolutions.Select((resolution, index) =>
            !resolution.HasDirectClaim && compatible[index].IsEmpty
                ? ConventionSourceMemberResolver.ResolveConstructorCaseInsensitiveFlattened(
                    context, parameters[index].Name, compilation, mapperType, cancellationToken)
                : ConventionSourceMemberResolution.None).ToImmutableArray();
        var fallback = FindCompatible(insensitive);
        var result = ImmutableArray.CreateBuilder<ResolvedConstructorSource>(parameters.Length);

        for (var index = 0; index < parameters.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (resolutions[index].HasDirectClaim)
            {
                var direct = resolutions[index].Candidates;
                result.Add(new ResolvedConstructorSource(
                    direct.Length == 1 ? direct[0] : null, Issue: null));
                continue;
            }

            var candidates = compatible[index].IsEmpty ? fallback[index] : compatible[index];
            var parameter = parameters[index];
            result.Add(new ResolvedConstructorSource(
                candidates.Length == 1 ? candidates[0] : null,
                candidates.Length > 1
                    ? ConventionMemberMappingPlanner.BuildFlatteningIssue(
                        parameter, parameter.Name, candidates, originNode)
                    : null));
        }

        return result.ToImmutable();

        ImmutableArray<ImmutableArray<ConventionReadableMember>> FindCompatible(
            ImmutableArray<ConventionSourceMemberResolution> candidates)
        {
            var primary = Probe(candidates.Select(candidate => candidate.HasDirectClaim
                ? ImmutableArray<ConventionReadableMember>.Empty : candidate.Candidates).ToImmutableArray());
            var secondary = Probe(candidates.Select((candidate, index) => primary[index].IsEmpty
                ? candidate.FallbackCandidates : ImmutableArray<ConventionReadableMember>.Empty).ToImmutableArray());
            return primary.Select((members, index) => members.IsEmpty ? secondary[index] : members)
                .ToImmutableArray();
        }

        ImmutableArray<ImmutableArray<ConventionReadableMember>> Probe(
            ImmutableArray<ImmutableArray<ConventionReadableMember>> candidates) =>
            ConventionSourceValueCompatibility.FindCompatibleCandidates(
                context.RootType,
                parameters.Select((parameter, index) => new ConventionSourceValueRequest(
                    ConventionConstructorMappingPlanner.GetParameterInputType(parameter), candidates[index]))
                    .ToImmutableArray(),
                compilation, mapperType, cancellationToken);
    }
}

internal readonly record struct ResolvedConstructorSource(
    ConventionReadableMember? Member,
    FlatteningIssueObservation? Issue);
