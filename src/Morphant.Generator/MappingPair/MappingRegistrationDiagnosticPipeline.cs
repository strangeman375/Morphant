using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperDeclaration;

namespace Morphant.Generator.MappingPair;

internal static class MappingRegistrationDiagnosticPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ImmutableArray<MapperContractAnalysis>>
            contractAnalyses)
    {
        var diagnostics = contractAnalyses
            .Select(static (analyses, cancellationToken) =>
                BuildDiagnostics(analyses, cancellationToken));

        DiagnosticPipeline.Register(context, diagnostics);
    }

    private static ImmutableArray<Diagnostic> BuildDiagnostics(
        ImmutableArray<MapperContractAnalysis> analyses,
        CancellationToken cancellationToken)
    {
        var candidates = ImmutableArray.CreateBuilder<DiagnosticCandidate>();
        var tuplePresentations =
            ImmutableArray.CreateBuilder<TuplePresentationRegistration>();
        var seenMappers = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);

        foreach (var analysis in analyses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var configuration = analysis.Configuration;
            var declaration = configuration.Declaration;

            if (!declaration.CanGenerateExecutableArtifact ||
                !seenMappers.Add(declaration.MapperType))
            {
                continue;
            }

            var model = configuration.MappingPairs;

            foreach (var pair in model.Pairs)
            {
                if (!BclTupleShapePolicy.ContainsTuplePresentation(
                        pair.SourceType) &&
                    !BclTupleShapePolicy.ContainsTuplePresentation(
                        pair.DestinationType))
                {
                    continue;
                }

                tuplePresentations.Add(
                    new TuplePresentationRegistration(
                        MappingTypeIdentityPolicy
                            .CreateAlphaEquivalentPairKey(
                                pair.SourceType,
                                pair.DestinationType),
                        BclTupleShapePolicy.BuildPairPresentationKey(
                            pair.SourceType,
                            pair.DestinationType),
                        pair,
                        declaration.MapperIdentity));
            }

            foreach (var pair in model.UnavailablePairs)
            {
                foreach (var unavailable in pair.UnavailableTypes)
                {
                    candidates.Add(new DiagnosticCandidate(
                        IdOrder: 11,
                        declaration.MapperIdentity,
                        PairKey(pair.Identity),
                        RoleOrder(unavailable.Role),
                        pair.Registration.Syntax.SpanStart,
                        SecondaryKey: string.Empty,
                        Diagnostic.Create(
                            MappingRegistrationDiagnosticDescriptors
                                .UnavailableMappingType,
                            GetTypeArgumentLocation(
                                pair.Registration.Syntax,
                                unavailable.Role),
                            GetRoleName(unavailable.Role),
                            MapperContractDisplay.CreateType(
                                unavailable.Type))));
                }
            }

            foreach (var pair in model.UnsupportedPairs)
            {
                foreach (var unsupported in pair.UnsupportedRoots)
                {
                    candidates.Add(new DiagnosticCandidate(
                        IdOrder: 12,
                        declaration.MapperIdentity,
                        PairKey(pair.Identity),
                        RoleOrder(unsupported.Role),
                        pair.Registration.Syntax.SpanStart,
                        SecondaryKey: string.Empty,
                        Diagnostic.Create(
                            MappingRegistrationDiagnosticDescriptors
                                .UnsupportedMappingRoot,
                            GetTypeArgumentLocation(
                                pair.Registration.Syntax,
                                unsupported.Role),
                            GetRoleName(unsupported.Role),
                            MapperContractDisplay.CreateType(
                                unsupported.Type),
                            unsupported.Reason)));
                }
            }

            foreach (var duplicate in model.DuplicateRegistrations)
            {
                candidates.Add(new DiagnosticCandidate(
                    IdOrder: 13,
                    declaration.MapperIdentity,
                    PairKey(duplicate.Identity),
                    RoleOrder: 0,
                    duplicate.Registration.Syntax.SpanStart,
                    SecondaryKey: string.Empty,
                    Diagnostic.Create(
                        MappingRegistrationDiagnosticDescriptors
                            .DuplicateRegistration,
                        GetMapIdentifierLocation(
                            duplicate.Registration.Syntax),
                        [GetMapIdentifierLocation(
                            duplicate.AuthoritativeRegistration.Syntax)],
                        properties: null,
                        MapperContractDisplay.Create(
                            duplicate.AuthoritativeRegistration.SourceType,
                            duplicate.AuthoritativeRegistration
                                .DestinationType),
                        declaration.MapperDisplayName)));
            }

            foreach (var conflict in analysis.GeneratedConflicts)
            {
                candidates.Add(new DiagnosticCandidate(
                    IdOrder: 14,
                    declaration.MapperIdentity,
                    PairKey(conflict.EarlierPairIdentity),
                    RoleOrder: 0,
                    conflict.LaterRegistration.Syntax.SpanStart,
                    SecondaryKey: PairKey(conflict.LaterPairIdentity),
                    Diagnostic.Create(
                        MappingRegistrationDiagnosticDescriptors
                            .UnifiableContracts,
                        GetMapIdentifierLocation(
                            conflict.LaterRegistration.Syntax),
                        [GetMapIdentifierLocation(
                            conflict.EarlierRegistration.Syntax)],
                        properties: null,
                        conflict.EarlierContractDisplayName,
                        conflict.LaterContractDisplayName,
                        declaration.MapperDisplayName)));
            }
        }

        AddTuplePresentationDiagnostics(
            tuplePresentations.ToImmutable(),
            candidates,
            cancellationToken);

        return candidates
            .OrderBy(static candidate => candidate.IdOrder)
            .ThenBy(static candidate => candidate.MapperIdentity,
                StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.PairKey,
                StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.RoleOrder)
            .ThenBy(static candidate => candidate.SecondaryKey,
                StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Position)
            .Select(static candidate => candidate.Diagnostic)
            .ToImmutableArray();
    }

    private static void AddTuplePresentationDiagnostics(
        ImmutableArray<TuplePresentationRegistration> registrations,
        ImmutableArray<DiagnosticCandidate>.Builder candidates,
        CancellationToken cancellationToken)
    {
        foreach (var physicalPair in registrations
                     .GroupBy(
                         static registration => registration.PhysicalPairKey,
                         StringComparer.Ordinal)
                     .OrderBy(
                         static group => group.Key,
                         StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var ordered = physicalPair
                .OrderBy(static registration =>
                    registration.Pair.Registration.Syntax.SyntaxTree
                        .FilePath,
                    StringComparer.Ordinal)
                .ThenBy(static registration =>
                    registration.Pair.Registration.Syntax.SpanStart)
                .ThenBy(static registration =>
                    registration.MapperIdentity,
                    StringComparer.Ordinal)
                .ThenBy(static registration =>
                    registration.PresentationKey,
                    StringComparer.Ordinal)
                .ToImmutableArray();
            var first = ordered[0];

            foreach (var registration in ordered.Skip(1))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (StringComparer.Ordinal.Equals(
                        first.PresentationKey,
                        registration.PresentationKey))
                {
                    continue;
                }

                candidates.Add(new DiagnosticCandidate(
                    IdOrder: 56,
                    registration.MapperIdentity,
                    physicalPair.Key,
                    RoleOrder: 0,
                    registration.Pair.Registration.Syntax.SpanStart,
                    SecondaryKey: registration.PresentationKey,
                    Diagnostic.Create(
                        MappingRegistrationDiagnosticDescriptors
                            .ConflictingTuplePresentation,
                        GetMapIdentifierLocation(
                            registration.Pair.Registration.Syntax),
                        [GetMapIdentifierLocation(
                            first.Pair.Registration.Syntax)],
                        properties: null,
                        MapperContractDisplay.Create(
                            registration.Pair.SourceType,
                            registration.Pair.DestinationType),
                        BuildTuplePresentationDisplay(registration.Pair),
                        BuildTuplePresentationDisplay(first.Pair))));
            }
        }
    }

    private static string BuildTuplePresentationDisplay(
        MappingPairModel pair)
    {
        return pair.SourceType.ToDisplayString(
                   SymbolDisplayFormats.FullyQualifiedNullable) +
               " -> " +
               pair.DestinationType.ToDisplayString(
                   SymbolDisplayFormats.FullyQualifiedNullable);
    }

    private static Location GetTypeArgumentLocation(
        InvocationExpressionSyntax invocation,
        MappingTypeRole role)
    {
        var genericName = invocation.Expression
            .DescendantNodesAndSelf()
            .OfType<GenericNameSyntax>()
            .First(name =>
                name.Identifier.ValueText == "Map" &&
                name.TypeArgumentList.Arguments.Count == 2);
        var index = role == MappingTypeRole.Source ? 0 : 1;

        return genericName.TypeArgumentList.Arguments[index].GetLocation();
    }

    private static Location GetMapIdentifierLocation(
        InvocationExpressionSyntax invocation)
    {
        var name = invocation.Expression
            .DescendantNodesAndSelf()
            .OfType<GenericNameSyntax>()
            .First(candidate =>
                candidate.Identifier.ValueText == "Map" &&
                candidate.TypeArgumentList.Arguments.Count == 2);

        return name.Identifier.GetLocation();
    }

    private static string PairKey(MappingPairIdentity identity)
    {
        return identity.Source.Key + "->" + identity.Destination.Key;
    }

    private static int RoleOrder(MappingTypeRole role)
    {
        return role == MappingTypeRole.Source ? 0 : 1;
    }

    private static string GetRoleName(MappingTypeRole role)
    {
        return role == MappingTypeRole.Source ? "source" : "destination";
    }

    private readonly record struct DiagnosticCandidate(
        int IdOrder,
        string MapperIdentity,
        string PairKey,
        int RoleOrder,
        int Position,
        string SecondaryKey,
        Diagnostic Diagnostic);

    private readonly record struct TuplePresentationRegistration(
        string PhysicalPairKey,
        string PresentationKey,
        MappingPairModel Pair,
        string MapperIdentity);
}
