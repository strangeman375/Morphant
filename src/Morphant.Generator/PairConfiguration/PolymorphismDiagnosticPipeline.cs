using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperDeclaration;

namespace Morphant.Generator.PairConfiguration;

internal static class PolymorphismDiagnosticPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ImmutableArray<MapperContractAnalysis>>
            contractAnalyses)
    {
        var diagnostics = contractAnalyses.Select(
            static (analyses, cancellationToken) =>
                BuildDiagnostics(analyses, cancellationToken));

        DiagnosticPipeline.Register(context, diagnostics);
    }

    private static ImmutableArray<Diagnostic> BuildDiagnostics(
        ImmutableArray<MapperContractAnalysis> analyses,
        CancellationToken cancellationToken)
    {
        var candidates = ImmutableArray.CreateBuilder<Candidate>();
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
                if (analysis.Excludes(pair.Pair.Identity))
                {
                    continue;
                }

                foreach (var issue in pair.Polymorphism.Issues)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var diagnostic = CreateDiagnostic(
                        issue,
                        pair,
                        declaration);

                    candidates.Add(new Candidate(
                        declaration.MapperIdentity,
                        pair.Pair.Identity.Source.Key + "->" +
                        pair.Pair.Identity.Destination.Key,
                        diagnostic.Location.SourceSpan.Start,
                        diagnostic));
                }
            }
        }

        return candidates
            .OrderBy(static candidate => candidate.MapperIdentity,
                StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.PairKey,
                StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Position)
            .ThenBy(static candidate => candidate.Diagnostic.Id,
                StringComparer.Ordinal)
            .Select(static candidate => candidate.Diagnostic)
            .ToImmutableArray();
    }

    private static Diagnostic CreateDiagnostic(
        PolymorphicConfigurationIssueModel issue,
        PairConfigurationModel pair,
        MapperDeclarationInfo declaration)
    {
        var link = issue.DerivedMapping;
        var contract = MapperContractDisplay.Create(
            pair.Pair.SourceType,
            pair.Pair.DestinationType);

        return issue.Kind switch
        {
            PolymorphicConfigurationIssueKind.SelfLink =>
                Diagnostic.Create(
                    PolymorphismDiagnosticDescriptors.SelfLink,
                    GetTypeArgumentLocation(link.Invocation, 0),
                    MapperContractDisplay.CreateType(link.SourceType),
                    contract),

            PolymorphicConfigurationIssueKind.DuplicateSource =>
                Diagnostic.Create(
                    PolymorphismDiagnosticDescriptors.DuplicateSource,
                    GetInvocationNameLocation(link.Invocation),
                    issue.FirstInvocation is null
                        ? null
                        : [GetInvocationNameLocation(issue.FirstInvocation)],
                    properties: null,
                    MapperContractDisplay.CreateType(link.SourceType),
                    contract),

            PolymorphicConfigurationIssueKind.IncompatibleSource =>
                CreateIncompatible(
                    link,
                    pair,
                    role: "source",
                    typeArgumentIndex: 0),

            PolymorphicConfigurationIssueKind.IncompatibleDestination =>
                CreateIncompatible(
                    link,
                    pair,
                    role: "destination",
                    typeArgumentIndex: 1),

            PolymorphicConfigurationIssueKind.InaccessibleSource =>
                CreateInaccessible(
                    link,
                    declaration,
                    role: "source",
                    typeArgumentIndex: 0),

            PolymorphicConfigurationIssueKind.InaccessibleDestination =>
                CreateInaccessible(
                    link,
                    declaration,
                    role: "destination",
                    typeArgumentIndex: 1),

            _ => throw new ArgumentOutOfRangeException(nameof(issue))
        };
    }

    private static Diagnostic CreateIncompatible(
        DerivedMappingConfigurationModel link,
        PairConfigurationModel pair,
        string role,
        int typeArgumentIndex)
    {
        var derivedType = typeArgumentIndex == 0
            ? link.SourceType
            : link.DestinationType;
        var baseType = typeArgumentIndex == 0
            ? pair.Pair.SourceType
            : pair.Pair.DestinationType;

        return Diagnostic.Create(
            PolymorphismDiagnosticDescriptors.IncompatibleType,
            GetTypeArgumentLocation(link.Invocation, typeArgumentIndex),
            role,
            MapperContractDisplay.CreateType(derivedType),
            MapperContractDisplay.CreateType(baseType),
            MapperContractDisplay.Create(
                pair.Pair.SourceType,
                pair.Pair.DestinationType));
    }

    private static Diagnostic CreateInaccessible(
        DerivedMappingConfigurationModel link,
        MapperDeclarationInfo declaration,
        string role,
        int typeArgumentIndex)
    {
        var type = typeArgumentIndex == 0
            ? link.SourceType
            : link.DestinationType;

        return Diagnostic.Create(
            PolymorphismDiagnosticDescriptors.InaccessibleType,
            GetTypeArgumentLocation(link.Invocation, typeArgumentIndex),
            role,
            MapperContractDisplay.CreateType(type),
            declaration.MapperDisplayName);
    }

    private static Location GetTypeArgumentLocation(
        InvocationExpressionSyntax invocation,
        int index)
    {
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax memberName
            } => memberName,
            GenericNameSyntax directName => directName,
            _ => throw new InvalidOperationException(
                "ForDerived must have a generic invocation name.")
        };

        return name
            .TypeArgumentList.Arguments[index]
            .GetLocation();
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

    private readonly record struct Candidate(
        string MapperIdentity,
        string PairKey,
        int Position,
        Diagnostic Diagnostic);
}
