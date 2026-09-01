using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.PairConfiguration;

internal static class MappingCompositionDiagnosticPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ImmutableArray<MapperContractAnalysis>>
            contractAnalyses)
    {
        var diagnostics = GeneratorStageGuard.Select(
            context,
            contractAnalyses,
            "BuildMappingCompositionDiagnostics",
            static (analyses, cancellationToken) =>
                BuildDiagnostics(analyses, cancellationToken),
            ImmutableArray<Diagnostic>.Empty);

        DiagnosticPipeline.Register(
            context,
            diagnostics,
            "MappingCompositionDiagnostics");
    }

    private static ImmutableArray<Diagnostic> BuildDiagnostics(
        ImmutableArray<MapperContractAnalysis> analyses,
        CancellationToken cancellationToken)
    {
        var candidates = ImmutableArray.CreateBuilder<DiagnosticCandidate>();
        var seenMappers = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);

        foreach (var analysis in analyses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var configuration = analysis.Configuration;
            var declaration = configuration.Declaration;

            if (!declaration.CanGenerateExecutableArtifact ||
                configuration.HasMapperWideConfigurationFlowFailure ||
                !seenMappers.Add(declaration.MapperType))
            {
                continue;
            }

            foreach (var pair in configuration.Pairs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (analysis.Excludes(pair.Pair.Identity) ||
                    HasMappingFlowFailure(configuration, pair.Pair.Identity))
                {
                    continue;
                }

                AddDuplicateDiagnostics(
                    candidates,
                    declaration,
                    pair,
                    cancellationToken);
                AddMixedDiagnostic(candidates, declaration, pair);
            }
        }

        return candidates
            .OrderBy(static candidate => candidate.IdOrder)
            .ThenBy(static candidate => candidate.MapperIdentity,
                StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.PairKey,
                StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Position)
            .Select(static candidate => candidate.Diagnostic)
            .ToImmutableArray();
    }

    private static void AddDuplicateDiagnostics(
        ImmutableArray<DiagnosticCandidate>.Builder candidates,
        MapperDeclarationInfo declaration,
        PairConfigurationModel pair,
        CancellationToken cancellationToken)
    {
        var firstBySlot = new Dictionary<MappingPlanSlotKind,
            MappingPlanSlotOccurrenceModel>();
        var pairKey = PairKey(pair.Pair.Identity);
        var contract = MapperContractDisplay.Create(
            pair.Pair.SourceType,
            pair.Pair.DestinationType);

        foreach (var occurrence in pair.LocalPlanSlots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (occurrence.Kind == MappingPlanSlotKind.IncludeMembers)
            {
                continue;
            }

            if (!firstBySlot.TryGetValue(occurrence.Kind, out var first))
            {
                firstBySlot.Add(occurrence.Kind, occurrence);
                continue;
            }

            var location = GetInvocationNameLocation(occurrence.Invocation);
            var firstLocation = GetInvocationNameLocation(
                first.Invocation);

            candidates.Add(new DiagnosticCandidate(
                IdOrder: 19,
                declaration.MapperIdentity,
                pairKey,
                location.SourceSpan.Start,
                Diagnostic.Create(
                    MappingCompositionDiagnosticDescriptors.DuplicatePlanSlot,
                    location,
                    [firstLocation],
                    properties: null,
                    SlotName(occurrence.Kind),
                    contract,
                    declaration.MapperDisplayName)));
        }
    }

    private static void AddMixedDiagnostic(
        ImmutableArray<DiagnosticCandidate>.Builder candidates,
        MapperDeclarationInfo declaration,
        PairConfigurationModel pair)
    {
        var firstResultPolicy = FindFirst(
            pair.LocalPlanSlots,
            MappingPlanSlotKind.ResultPolicy);
        var firstMembers = FindFirst(
            pair.LocalPlanSlots,
            MappingPlanSlotKind.Members);
        var firstIncludeMembers = FindFirst(
            pair.LocalPlanSlots,
            MappingPlanSlotKind.IncludeMembers);
        var firstConvert = FindFirst(
            pair.LocalPlanSlots,
            MappingPlanSlotKind.Convert);

        if (firstConvert is null ||
            firstResultPolicy is null &&
            firstMembers is null &&
            firstIncludeMembers is null)
        {
            return;
        }

        var firstDeclarative = Earlier(
            Earlier(firstResultPolicy, firstMembers),
            firstIncludeMembers)!;
        var primary = IsEarlier(firstConvert.Value, firstDeclarative.Value)
            ? firstDeclarative.Value
            : firstConvert.Value;
        var additionalLocations =
            ImmutableArray.CreateBuilder<Location>(4);

        AddLocation(firstResultPolicy);
        AddLocation(firstMembers);
        AddLocation(firstIncludeMembers);
        AddLocation(firstConvert);

        var primaryLocation = GetInvocationNameLocation(primary.Invocation);

        candidates.Add(new DiagnosticCandidate(
            IdOrder: 20,
            declaration.MapperIdentity,
            PairKey(pair.Pair.Identity),
            primaryLocation.SourceSpan.Start,
            Diagnostic.Create(
                MappingCompositionDiagnosticDescriptors
                    .MixedConvertAndDeclarative,
                primaryLocation,
                additionalLocations.ToImmutable(),
                properties: null,
                MapperContractDisplay.Create(
                    pair.Pair.SourceType,
                    pair.Pair.DestinationType),
                declaration.MapperDisplayName)));

        void AddLocation(MappingPlanSlotOccurrenceModel? occurrence)
        {
            if (occurrence is { } value)
            {
                additionalLocations.Add(
                    GetInvocationNameLocation(value.Invocation));
            }
        }
    }

    private static MappingPlanSlotOccurrenceModel? FindFirst(
        ImmutableArray<MappingPlanSlotOccurrenceModel> occurrences,
        MappingPlanSlotKind kind)
    {
        foreach (var occurrence in occurrences)
        {
            if (occurrence.Kind == kind)
            {
                return occurrence;
            }
        }

        return null;
    }

    private static MappingPlanSlotOccurrenceModel? Earlier(
        MappingPlanSlotOccurrenceModel? left,
        MappingPlanSlotOccurrenceModel? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return IsEarlier(left.Value, right.Value) ? left : right;
    }

    private static bool IsEarlier(
        MappingPlanSlotOccurrenceModel left,
        MappingPlanSlotOccurrenceModel right)
    {
        return GetInvocationNameLocation(left.Invocation).SourceSpan.Start <
               GetInvocationNameLocation(right.Invocation).SourceSpan.Start;
    }

    private static bool HasMappingFlowFailure(
        MapperPairConfigurationModel configuration,
        MappingPairIdentity identity)
    {
        return configuration.FlowBreaks.Any(flowBreak =>
            flowBreak.Kind == BuilderFlowBreakKind.Mapping &&
            flowBreak.Registration is { } registration &&
            IsIdentity(registration, identity) &&
            !IsDiscardedDuplicate(configuration, registration));
    }

    private static bool IsIdentity(
        MappingPairRegistrationModel registration,
        MappingPairIdentity identity)
    {
        var registrationIdentity = new MappingPairIdentity(
            MappingTypeIdentityPolicy.Create(registration.SourceType),
            MappingTypeIdentityPolicy.Create(registration.DestinationType));

        return StringComparer.Ordinal.Equals(
                   registrationIdentity.Source.Key,
                   identity.Source.Key) &&
               StringComparer.Ordinal.Equals(
                   registrationIdentity.Destination.Key,
                   identity.Destination.Key);
    }

    private static bool IsDiscardedDuplicate(
        MapperPairConfigurationModel configuration,
        MappingPairRegistrationModel registration)
    {
        return configuration.SurfaceMappingPairs.Any(model =>
            model.DuplicateRegistrations.Any(duplicate =>
                duplicate.Registration.Syntax.SyntaxTree ==
                    registration.Syntax.SyntaxTree &&
                duplicate.Registration.Syntax.Span ==
                    registration.Syntax.Span));
    }

    private static Location GetInvocationNameLocation(
        InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            SimpleNameSyntax name => name.Identifier.GetLocation(),
            MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Name.Identifier.GetLocation(),
            MemberBindingExpressionSyntax memberBinding =>
                memberBinding.Name.Identifier.GetLocation(),
            _ => invocation.GetLocation()
        };
    }

    private static string SlotName(MappingPlanSlotKind kind)
    {
        return kind switch
        {
            MappingPlanSlotKind.ResultPolicy => "Construct or Resolve",
            MappingPlanSlotKind.Members => "Members",
            MappingPlanSlotKind.IncludeMembers => "IncludeMembers",
            MappingPlanSlotKind.Convert => "Convert",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static string PairKey(MappingPairIdentity identity)
    {
        return identity.Source.Key + "->" + identity.Destination.Key;
    }

    private readonly record struct DiagnosticCandidate(
        int IdOrder,
        string MapperIdentity,
        string PairKey,
        int Position,
        Diagnostic Diagnostic);
}
