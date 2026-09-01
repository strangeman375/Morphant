using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.PairConfiguration;

internal static class InheritanceDiagnosticPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ImmutableArray<MapperContractAnalysis>>
            contractAnalyses)
    {
        var diagnostics = GeneratorStageGuard.Select(
            context,
            contractAnalyses,
            "BuildInheritanceDiagnostics",
            static (analyses, cancellationToken) =>
                BuildDiagnostics(analyses, cancellationToken),
            ImmutableArray<Diagnostic>.Empty);

        DiagnosticPipeline.Register(
            context,
            diagnostics,
            "InheritanceDiagnostics");
    }

    private static ImmutableArray<Diagnostic> BuildDiagnostics(
        ImmutableArray<MapperContractAnalysis> analyses,
        CancellationToken cancellationToken)
    {
        var candidates = ImmutableArray.CreateBuilder<DiagnosticCandidate>();
        var seenMappers = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);
        var seenBaseDuplicates = new HashSet<SourceOriginKey>();
        var seenIncludeDuplicates = new HashSet<SourceOriginKey>();
        var seenConstructedIssues = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (var analysis in analyses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var configuration = analysis.Configuration;
            var declaration = configuration.Declaration;

            if (!declaration.CanGenerateExecutableArtifact ||
                !seenMappers.Add(declaration.MapperType) ||
                HasMapperFlowFailure(configuration))
            {
                continue;
            }

            AddDuplicateBaseConfigurationDiagnostics(
                configuration,
                candidates,
                seenBaseDuplicates);

            foreach (var pair in configuration.Pairs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (analysis.Excludes(pair.Pair.Identity) ||
                    HasMappingFlowFailure(
                        configuration,
                        pair.Pair.Identity))
                {
                    continue;
                }

                foreach (var issue in pair.Composition.Issues)
                {
                    AddCompositionIssueDiagnostic(
                        issue,
                        declaration,
                        candidates,
                        seenIncludeDuplicates,
                        seenConstructedIssues);
                }

                AddAccessibilityDiagnostics(
                    pair,
                    declaration,
                    candidates);
            }
        }

        return candidates
            .OrderBy(static candidate => candidate.IdOrder)
            .ThenBy(static candidate => candidate.MapperIdentity,
                StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.LevelOrder)
            .ThenBy(static candidate => candidate.PairKey,
                StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Position)
            .ThenBy(static candidate => candidate.Detail,
                StringComparer.Ordinal)
            .Select(static candidate => candidate.Diagnostic)
            .ToImmutableArray();
    }

    private static void AddDuplicateBaseConfigurationDiagnostics(
        MapperPairConfigurationModel configuration,
        ImmutableArray<DiagnosticCandidate>.Builder candidates,
        ISet<SourceOriginKey> seen)
    {
        foreach (var duplicate in
                 configuration.DuplicateBaseConfigurationCalls)
        {
            var key = SourceOriginKey.Create(duplicate.DuplicateInvocation);

            if (!seen.Add(key))
            {
                continue;
            }

            var location = GetInvocationNameLocation(
                duplicate.DuplicateInvocation);
            var mapper = duplicate.DeclaringMapperType.OriginalDefinition;

            candidates.Add(new DiagnosticCandidate(
                IdOrder: 24,
                MapperIdentity: SymbolNameHelper.GetFullMetadataName(mapper),
                duplicate.LevelOrder,
                PairKey: string.Empty,
                location.SourceSpan.Start,
                Detail: string.Empty,
                Diagnostic.Create(
                    InheritanceDiagnosticDescriptors
                        .DuplicateBaseConfiguration,
                    location,
                    [GetInvocationNameLocation(duplicate.FirstInvocation)],
                    properties: null,
                    MapperContractDisplay.CreateType(mapper))));
        }
    }

    private static void AddCompositionIssueDiagnostic(
        InheritanceCompositionIssueModel issue,
        MapperDeclarationInfo finalMapper,
        ImmutableArray<DiagnosticCandidate>.Builder candidates,
        ISet<SourceOriginKey> seenIncludeDuplicates,
        ISet<string> seenConstructedIssues)
    {
        switch (issue.Kind)
        {
            case InheritanceCompositionIssueKind.DuplicateIncludeBase:
                AddDuplicateIncludeBaseDiagnostic(
                    issue,
                    candidates,
                    seenIncludeDuplicates);
                return;

            case InheritanceCompositionIssueKind.MissingIncludedPair:
                AddMissingPairDiagnostic(
                    issue,
                    finalMapper,
                    candidates,
                    seenConstructedIssues);
                return;

            case InheritanceCompositionIssueKind.IncompatibleSource:
                AddIncompatibleTypeDiagnostic(
                    issue,
                    finalMapper,
                    MappingTypeRole.Source,
                    candidates,
                    seenConstructedIssues);
                return;

            case InheritanceCompositionIssueKind.IncompatibleDestination:
                AddIncompatibleTypeDiagnostic(
                    issue,
                    finalMapper,
                    MappingTypeRole.Destination,
                    candidates,
                    seenConstructedIssues);
                return;

            case InheritanceCompositionIssueKind.InvalidIncludedPair:
                return;

            default:
                throw new ArgumentOutOfRangeException(nameof(issue.Kind));
        }
    }

    private static void AddDuplicateIncludeBaseDiagnostic(
        InheritanceCompositionIssueModel issue,
        ImmutableArray<DiagnosticCandidate>.Builder candidates,
        ISet<SourceOriginKey> seen)
    {
        var location = GetInvocationNameLocation(issue.IncludeBase.Invocation);

        if (!seen.Add(SourceOriginKey.Create(issue.IncludeBase.Invocation)))
        {
            return;
        }

        var sourceMapper = issue.Origin.DeclaringMapperType.OriginalDefinition;
        var pairKey = PairKey(
            issue.Origin.DeclaredRegistration.SourceType,
            issue.Origin.DeclaredRegistration.DestinationType);

        candidates.Add(new DiagnosticCandidate(
            IdOrder: 25,
            MapperIdentity: SymbolNameHelper.GetFullMetadataName(sourceMapper),
            issue.Origin.LevelOrder,
            pairKey,
            location.SourceSpan.Start,
            Detail: string.Empty,
            Diagnostic.Create(
                InheritanceDiagnosticDescriptors.DuplicateIncludeBase,
                location,
                [GetInvocationNameLocation(issue.FirstInvocation!)],
                properties: null,
                MapperContractDisplay.Create(
                    issue.Origin.DeclaredRegistration.SourceType,
                    issue.Origin.DeclaredRegistration.DestinationType),
                MapperContractDisplay.CreateType(sourceMapper))));
    }

    private static void AddMissingPairDiagnostic(
        InheritanceCompositionIssueModel issue,
        MapperDeclarationInfo finalMapper,
        ImmutableArray<DiagnosticCandidate>.Builder candidates,
        ISet<string> seen)
    {
        var identity = ConstructedIssueIdentity(issue, finalMapper, "missing");

        if (!seen.Add(identity))
        {
            return;
        }

        var location = GetInvocationNameLocation(issue.IncludeBase.Invocation);
        var pairKey = PairKey(
            issue.Origin.Registration.SourceType,
            issue.Origin.Registration.DestinationType);

        candidates.Add(new DiagnosticCandidate(
            IdOrder: 26,
            finalMapper.MapperIdentity,
            issue.Origin.LevelOrder,
            pairKey,
            location.SourceSpan.Start,
            Detail: string.Empty,
            Diagnostic.Create(
                InheritanceDiagnosticDescriptors.IncludedPairNotFound,
                location,
                MapperContractDisplay.Create(
                    issue.IncludeBase.SourceType,
                    issue.IncludeBase.DestinationType),
                MapperContractDisplay.Create(
                    issue.Origin.Registration.SourceType,
                    issue.Origin.Registration.DestinationType),
                finalMapper.MapperDisplayName)));
    }

    private static void AddIncompatibleTypeDiagnostic(
        InheritanceCompositionIssueModel issue,
        MapperDeclarationInfo finalMapper,
        MappingTypeRole role,
        ImmutableArray<DiagnosticCandidate>.Builder candidates,
        ISet<string> seen)
    {
        var roleName = role == MappingTypeRole.Source
            ? "source"
            : "destination";
        var identity = ConstructedIssueIdentity(
            issue,
            finalMapper,
            roleName);

        if (!seen.Add(identity))
        {
            return;
        }

        var typeArgumentIndex = role == MappingTypeRole.Source ? 0 : 1;
        var location = GetTypeArgumentLocation(
            issue.IncludeBase.Invocation,
            typeArgumentIndex);
        var currentType = role == MappingTypeRole.Source
            ? issue.Origin.Registration.SourceType
            : issue.Origin.Registration.DestinationType;
        var includedType = role == MappingTypeRole.Source
            ? issue.IncludeBase.SourceType
            : issue.IncludeBase.DestinationType;
        var pairKey = PairKey(
            issue.Origin.Registration.SourceType,
            issue.Origin.Registration.DestinationType);

        candidates.Add(new DiagnosticCandidate(
            IdOrder: 27,
            finalMapper.MapperIdentity,
            issue.Origin.LevelOrder,
            pairKey,
            location.SourceSpan.Start,
            roleName,
            Diagnostic.Create(
                InheritanceDiagnosticDescriptors.IncompatibleIncludedType,
                location,
                [GetTypeArgumentLocation(
                    issue.Origin.Registration.Syntax,
                    typeArgumentIndex)],
                properties: null,
                roleName,
                MapperContractDisplay.CreateType(currentType),
                MapperContractDisplay.CreateType(includedType),
                MapperContractDisplay.Create(
                    issue.Origin.Registration.SourceType,
                    issue.Origin.Registration.DestinationType),
                finalMapper.MapperDisplayName)));
    }

    private static void AddAccessibilityDiagnostics(
        PairConfigurationModel pair,
        MapperDeclarationInfo finalMapper,
        ImmutableArray<DiagnosticCandidate>.Builder candidates)
    {
        if (pair.Composition.InaccessibleCallbacks.IsEmpty ||
            pair.Composition.IncludeBaseCalls.Length != 1)
        {
            return;
        }

        var primary = GetInvocationNameLocation(
            pair.Composition.IncludeBaseCalls[0].Invocation);
        var pairKey = PairKey(pair.Pair.SourceType, pair.Pair.DestinationType);
        var contract = MapperContractDisplay.Create(
            pair.Pair.SourceType,
            pair.Pair.DestinationType);

        foreach (var callback in pair.Composition.InaccessibleCallbacks)
        {
            var additionalLocations = ImmutableArray.CreateBuilder<Location>(
                callback.ReferenceLocations.Length + 1);
            additionalLocations.Add(GetInvocationNameLocation(
                callback.Invocation));
            additionalLocations.AddRange(callback.ReferenceLocations);

            candidates.Add(new DiagnosticCandidate(
                IdOrder: 28,
                finalMapper.MapperIdentity,
                callback.LevelOrder,
                pairKey,
                primary.SourceSpan.Start,
                callback.CallbackName,
                Diagnostic.Create(
                    InheritanceDiagnosticDescriptors
                        .InaccessibleInheritedCallback,
                    primary,
                    additionalLocations.ToImmutable(),
                    properties: null,
                    callback.CallbackName,
                    contract,
                    finalMapper.MapperDisplayName)));
        }
    }

    private static string ConstructedIssueIdentity(
        InheritanceCompositionIssueModel issue,
        MapperDeclarationInfo finalMapper,
        string detail)
    {
        return finalMapper.MapperIdentity + "|" +
               issue.Origin.ConstructedMapperType.ToDisplayString(
                   SymbolDisplayFormats.FullyQualifiedNullable) + "|" +
               PairKey(
                   issue.Origin.Registration.SourceType,
                   issue.Origin.Registration.DestinationType) + "|" +
               issue.IncludeBase.Invocation.SyntaxTree.FilePath + "|" +
               issue.IncludeBase.Invocation.SpanStart + "|" + detail;
    }

    private static bool HasMapperFlowFailure(
        MapperPairConfigurationModel configuration)
    {
        return configuration.FlowBreaks.Any(static flowBreak =>
            flowBreak.Kind == BuilderFlowBreakKind.Mapper);
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

    private static Location GetTypeArgumentLocation(
        InvocationExpressionSyntax invocation,
        int index)
    {
        var genericName = invocation.Expression switch
        {
            GenericNameSyntax directName => directName,
            MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax memberName
            } => memberName,
            MemberBindingExpressionSyntax
            {
                Name: GenericNameSyntax memberName
            } => memberName,
            _ => null
        };

        return genericName is not null &&
               genericName.TypeArgumentList.Arguments.Count > index
            ? genericName.TypeArgumentList.Arguments[index].GetLocation()
            : invocation.GetLocation();
    }

    private static string PairKey(
        ITypeSymbol source,
        ITypeSymbol destination)
    {
        return MappingTypeIdentityPolicy.Create(source).Key + "->" +
               MappingTypeIdentityPolicy.Create(destination).Key;
    }

    private readonly record struct SourceOriginKey(
        SyntaxTree SyntaxTree,
        int Start,
        int Length)
    {
        public static SourceOriginKey Create(SyntaxNode node) =>
            new(node.SyntaxTree, node.SpanStart, node.Span.Length);
    }

    private readonly record struct DiagnosticCandidate(
        int IdOrder,
        string MapperIdentity,
        int LevelOrder,
        string PairKey,
        int Position,
        string Detail,
        Diagnostic Diagnostic);
}
