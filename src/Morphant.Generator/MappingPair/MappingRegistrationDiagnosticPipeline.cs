using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperDeclaration;

namespace Morphant.Generator.MappingPair;

internal static class MappingRegistrationDiagnosticPipeline
{
    public static IncrementalValuesProvider<MapperContractAnalysis> CheckRegistrations(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<MapperContractAnalysis> analyses,
        bool reportDiagnostics)
    {
        var checks = GeneratorStageGuard.Select(
            context, analyses.Collect(), "BuildMappingRegistrationDiagnostics",
            static (all, token) => CheckRegistrations(all, token),
            new RegistrationChecks(ImmutableArray<Diagnostic>.Empty,
                ImmutableDictionary<string, ImmutableHashSet<string>>.Empty));
        if (reportDiagnostics)
        {
            DiagnosticPipeline.Register(context, checks.Select(static (result, _) => result.Diagnostics),
                "MappingRegistrationDiagnostics");
        }

        var conflicts = checks.Select(static (result, _) => result.Conflicts);
        return analyses.Combine(conflicts).Select(static (input, _) =>
            input.Right.TryGetValue(input.Left.Configuration.Declaration.MapperIdentity, out var pairs)
                ? input.Left with { ConflictingSurfaces = pairs }
                : input.Left);
    }

    private sealed record RegistrationChecks(
        ImmutableArray<Diagnostic> Diagnostics,
        ImmutableDictionary<string, ImmutableHashSet<string>> Conflicts);

    private static RegistrationChecks CheckRegistrations(
        ImmutableArray<MapperContractAnalysis> analyses,
        CancellationToken cancellationToken)
    {
        var conflicts = ImmutableDictionary.CreateBuilder<string, ImmutableHashSet<string>>(StringComparer.Ordinal);
        var candidates = ImmutableArray.CreateBuilder<DiagnosticCandidate>();
        var presentations =
            ImmutableArray.CreateBuilder<PresentationRegistration>();
        var openRegistrations = new Dictionary<string, (MappingPairModel Pair, string Mapper)>(StringComparer.Ordinal);
        var reported = new HashSet<(SyntaxTree Tree, int Start, string Pair)>();
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

            var surfaceCandidates = CanonicalMappingPairPipeline
                .BuildCandidates(
                    model.MapperIdentity,
                    declaration.MapperType,
                    configuration.SurfaceMappingPairs,
                    declaration.Compilation);

            foreach (var surfaceCandidate in surfaceCandidates)
            {
                var pair = surfaceCandidate.Pair;

                var physicalPair = MappingTypeIdentityPolicy.CreateAlphaEquivalentPairKey(
                    pair.SourceType, pair.DestinationType);
                if (pair.FriendAssembly is { } friend &&
                    reported.Add((pair.Registration.Syntax.SyntaxTree, pair.Registration.Syntax.SpanStart, physicalPair)))
                {
                    candidates.Add(new DiagnosticCandidate(
                        65, declaration.MapperIdentity, physicalPair, 0, pair.Registration.Syntax.SpanStart, friend,
                        Diagnostic.Create(MappingRegistrationDiagnosticDescriptors.FriendExtensionConflict,
                            GetMapIdentifierLocation(pair.Registration.Syntax),
                            MapperContractDisplay.Create(pair.SourceType, pair.DestinationType), friend)));
                }

                if (!ConstructionSurface.GeneratedTypeNameBuilder.CollectTypeParameters(
                        pair.SourceType, pair.DestinationType).IsEmpty && !openRegistrations.ContainsKey(physicalPair))
                {
                    openRegistrations.Add(physicalPair, (pair, model.MapperIdentity));
                }

                presentations.Add(
                    new PresentationRegistration(
                        MappingTypeIdentityPolicy
                            .CreateAlphaEquivalentPairKey(
                                surfaceCandidate.EffectiveSourceType,
                                surfaceCandidate.EffectiveDestinationType),
                        BclTupleShapePolicy.BuildPairPresentationKey(
                            surfaceCandidate.EffectiveSourceType,
                            surfaceCandidate.EffectiveDestinationType),
                        pair,
                        declaration.MapperIdentity,
                        surfaceCandidate.EffectiveSourceType,
                        surfaceCandidate.EffectiveDestinationType));
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
                if (HasDifferingPresentation(
                        duplicate.AuthoritativeRegistration,
                        duplicate.Registration))
                {
                    candidates.Add(new DiagnosticCandidate(
                        IdOrder: 56,
                        declaration.MapperIdentity,
                        PairKey(duplicate.Identity),
                        RoleOrder: 0,
                        duplicate.Registration.Syntax.SpanStart,
                        SecondaryKey:
                            BclTupleShapePolicy.BuildPairPresentationKey(
                                duplicate.Registration.SourceType,
                                duplicate.Registration.DestinationType),
                        Diagnostic.Create(
                            MappingRegistrationDiagnosticDescriptors
                                .ConflictingPresentation,
                            GetMapIdentifierLocation(
                                duplicate.Registration.Syntax),
                            [GetMapIdentifierLocation(
                                duplicate.AuthoritativeRegistration.Syntax)],
                            properties: null,
                            MapperContractDisplay.Create(
                                duplicate.Registration.SourceType,
                                duplicate.Registration.DestinationType),
                            BuildPresentationDisplay(
                                duplicate.Registration),
                            BuildPresentationDisplay(
                                duplicate.AuthoritativeRegistration))));
                    continue;
                }

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

        AddOpenExtensionDiagnostics(openRegistrations, candidates, conflicts, cancellationToken);
        AddPresentationDiagnostics(
            presentations.ToImmutable(),
            candidates,
            conflicts,
            cancellationToken);

        var diagnostics = candidates
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
        return new RegistrationChecks(diagnostics, conflicts.Count == 0
            ? ImmutableDictionary<string, ImmutableHashSet<string>>.Empty
            : conflicts.ToImmutable());
    }

    private static void MarkConflict(
        ImmutableDictionary<string, ImmutableHashSet<string>>.Builder conflicts, string mapper, string pair)
    {
        conflicts[mapper] = (conflicts.TryGetValue(mapper, out var pairs) ? pairs : ImmutableHashSet<string>.Empty).Add(pair);
    }

    private static void AddOpenExtensionDiagnostics(
        Dictionary<string, (MappingPairModel Pair, string Mapper)> registrations,
        ImmutableArray<DiagnosticCandidate>.Builder diagnostics,
        ImmutableDictionary<string, ImmutableHashSet<string>>.Builder conflicts,
        CancellationToken cancellationToken)
    {
        var ordered = registrations.OrderBy(entry => entry.Key, StringComparer.Ordinal).ToArray();
        for (var later = 1; later < ordered.Length; later++)
        {
            var (right, rightMapper) = ordered[later].Value;
            for (var earlier = 0; earlier < later; earlier++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (left, leftMapper) = ordered[earlier].Value;
                if (leftMapper == rightMapper ||
                    !MappingTypeIdentityPolicy.CanPairsUnify(left.SourceType, left.DestinationType,
                        right.SourceType, right.DestinationType) ||
                    MappingTypeIdentityPolicy.AcceptsPair(left, right) ||
                    MappingTypeIdentityPolicy.AcceptsPair(right, left))
                    continue;

                MarkConflict(conflicts, rightMapper, ordered[later].Key);
                MarkConflict(conflicts, leftMapper, ordered[earlier].Key);
                diagnostics.Add(new DiagnosticCandidate(
                    64, rightMapper, ordered[later].Key, 0, right.Registration.Syntax.SpanStart, ordered[earlier].Key,
                    Diagnostic.Create(MappingRegistrationDiagnosticDescriptors.AmbiguousOpenExtensions,
                        GetMapIdentifierLocation(right.Registration.Syntax),
                        [GetMapIdentifierLocation(left.Registration.Syntax)], properties: null,
                        MapperContractDisplay.Create(left.SourceType, left.DestinationType),
                        MapperContractDisplay.Create(right.SourceType, right.DestinationType))));
            }
        }
    }

    private static void AddPresentationDiagnostics(
        ImmutableArray<PresentationRegistration> registrations,
        ImmutableArray<DiagnosticCandidate>.Builder candidates,
        ImmutableDictionary<string, ImmutableHashSet<string>>.Builder conflicts,
        CancellationToken cancellationToken)
    {
        foreach (var physicalPair in registrations
                     .GroupBy(
                         static registration =>
                             registration.PhysicalPairKey,
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

                MarkConflict(conflicts, registration.MapperIdentity, physicalPair.Key);
                candidates.Add(new DiagnosticCandidate(
                    IdOrder: 56,
                    registration.MapperIdentity,
                    physicalPair.Key,
                    RoleOrder: 0,
                    registration.Pair.Registration.Syntax.SpanStart,
                    SecondaryKey: registration.PresentationKey,
                    Diagnostic.Create(
                        MappingRegistrationDiagnosticDescriptors
                            .ConflictingPresentation,
                        GetMapIdentifierLocation(
                            registration.Pair.Registration.Syntax),
                        [GetMapIdentifierLocation(
                            first.Pair.Registration.Syntax)],
                        properties: null,
                        MapperContractDisplay.Create(
                            registration.EffectiveSourceType,
                            registration.EffectiveDestinationType),
                        BuildPresentationDisplay(
                            registration.EffectiveSourceType,
                            registration.EffectiveDestinationType),
                        BuildPresentationDisplay(
                            first.EffectiveSourceType,
                            first.EffectiveDestinationType))));
            }
        }
    }

    private static bool HasDifferingPresentation(
        MappingPairRegistrationModel first,
        MappingPairRegistrationModel second)
    {
        return !StringComparer.Ordinal.Equals(
            BclTupleShapePolicy.BuildPairPresentationKey(
                first.SourceType,
                first.DestinationType),
            BclTupleShapePolicy.BuildPairPresentationKey(
                second.SourceType,
                second.DestinationType));
    }

    private static string BuildPresentationDisplay(
        MappingPairRegistrationModel registration)
    {
        return BuildPresentationDisplay(
            registration.SourceType,
            registration.DestinationType);
    }

    private static string BuildPresentationDisplay(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        return sourceType.ToDisplayString(
                   SymbolDisplayFormats.FullyQualifiedNullable) +
               " -> " +
               destinationType.ToDisplayString(
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

    private readonly record struct PresentationRegistration(
        string PhysicalPairKey,
        string PresentationKey,
        MappingPairModel Pair,
        string MapperIdentity,
        ITypeSymbol EffectiveSourceType,
        ITypeSymbol EffectiveDestinationType);
}
