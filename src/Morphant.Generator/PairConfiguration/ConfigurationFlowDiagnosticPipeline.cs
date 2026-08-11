using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.MappingPair;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.PairConfiguration;

internal static class ConfigurationFlowDiagnosticPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<MapperConfigureDeclarationInfo>
            configureDeclarations,
        IncrementalValuesProvider<MapperContractAnalysis> contractAnalyses)
    {
        var diagnostics = configureDeclarations
            .Collect()
            .Combine(contractAnalyses.Collect())
            .Select(static (source, cancellationToken) =>
                BuildDiagnostics(
                    source.Left,
                    source.Right,
                    cancellationToken));

        context.RegisterSourceOutput(
            diagnostics,
            static (productionContext, values) =>
            {
                foreach (var diagnostic in values)
                {
                    productionContext.ReportDiagnostic(diagnostic);
                }
            });
    }

    private static ImmutableArray<Diagnostic> BuildDiagnostics(
        ImmutableArray<MapperConfigureDeclarationInfo> configureDeclarations,
        ImmutableArray<MapperContractAnalysis> contractAnalyses,
        CancellationToken cancellationToken)
    {
        var candidates = ImmutableArray.CreateBuilder<DiagnosticCandidate>();
        var seenMappers = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);

        foreach (var configure in configureDeclarations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var declaration = configure.Declaration;

            if (!declaration.CanGenerateExecutableArtifact ||
                !seenMappers.Add(declaration.MapperType) ||
                configure.State is not (
                    MapperConfigureDeclarationState.Missing or
                    MapperConfigureDeclarationState.Bodyless))
            {
                continue;
            }

            candidates.Add(new DiagnosticCandidate(
                IdOrder: 15,
                declaration.MapperIdentity,
                LevelOrder: 0,
                PairKey: string.Empty,
                configure.MissingConfigureLocation.SourceSpan.Start,
                Diagnostic.Create(
                    ConfigurationFlowDiagnosticDescriptors.MissingConfigure,
                    configure.MissingConfigureLocation,
                    declaration.MapperDisplayName)));
        }

        seenMappers.Clear();

        foreach (var analysis in contractAnalyses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var configuration = analysis.Configuration;
            var declaration = configuration.Declaration;

            if (!declaration.CanGenerateExecutableArtifact ||
                !seenMappers.Add(declaration.MapperType))
            {
                continue;
            }

            foreach (var unavailable in
                     configuration.UnavailableBaseConfigurations)
            {
                candidates.Add(new DiagnosticCandidate(
                    IdOrder: 16,
                    declaration.MapperIdentity,
                    unavailable.LevelOrder,
                    PairKey: string.Empty,
                    unavailable.Invocation.SpanStart,
                    Diagnostic.Create(
                        ConfigurationFlowDiagnosticDescriptors
                            .UnavailableBaseConfigure,
                        GetInvocationNameLocation(
                            unavailable.Invocation),
                        unavailable.BaseMapperType.ToDisplayString(
                            SymbolDisplayFormats.FullyQualifiedNullable),
                        declaration.MapperDisplayName)));
            }

            foreach (var flowBreak in configuration.FlowBreaks)
            {
                if (flowBreak.Kind != BuilderFlowBreakKind.Mapper)
                {
                    continue;
                }

                candidates.Add(new DiagnosticCandidate(
                    IdOrder: 17,
                    declaration.MapperIdentity,
                    flowBreak.LevelOrder,
                    PairKey: string.Empty,
                    flowBreak.Location.SourceSpan.Start,
                    Diagnostic.Create(
                        ConfigurationFlowDiagnosticDescriptors
                            .UnsupportedMapperFlow,
                        flowBreak.Location,
                        declaration.MapperDisplayName)));
            }

            foreach (var flowBreak in configuration.FlowBreaks)
            {
                if (flowBreak.Kind != BuilderFlowBreakKind.Mapping ||
                    flowBreak.Registration is not { } registration ||
                    IsDiscardedDuplicate(
                        configuration,
                        registration) ||
                    IsCompilerOwned(registration, configuration))
                {
                    continue;
                }

                var identity = new MappingPairIdentity(
                    MappingTypeIdentityPolicy.Create(registration.SourceType),
                    MappingTypeIdentityPolicy.Create(
                        registration.DestinationType));

                candidates.Add(new DiagnosticCandidate(
                    IdOrder: 18,
                    declaration.MapperIdentity,
                    flowBreak.LevelOrder,
                    PairKey(identity),
                    flowBreak.Location.SourceSpan.Start,
                    Diagnostic.Create(
                        ConfigurationFlowDiagnosticDescriptors
                            .UnsupportedMappingFlow,
                        flowBreak.Location,
                        MapperContractDisplay.Create(
                            registration.SourceType,
                            registration.DestinationType),
                        declaration.MapperDisplayName)));
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
            .Select(static candidate => candidate.Diagnostic)
            .ToImmutableArray();
    }

    private static bool IsDiscardedDuplicate(
        MapperPairConfigurationModel configuration,
        MappingPairRegistrationModel registration)
    {
        return configuration.SurfaceMappingPairs.Any(model =>
            model.DuplicateRegistrations.Any(duplicate =>
                IsSameInvocation(
                    duplicate.Registration.Syntax,
                    registration.Syntax)));
    }

    private static bool IsCompilerOwned(
        MappingPairRegistrationModel registration,
        MapperPairConfigurationModel configuration)
    {
        return !configuration.SurfaceMappingPairs
            .SelectMany(EnumerateRegistrations)
            .Any(candidate => IsSameInvocation(
                candidate.Syntax,
                registration.Syntax));
    }

    private static IEnumerable<MappingPairRegistrationModel>
        EnumerateRegistrations(MapperMappingPairModel model)
    {
        return model.Pairs
            .Select(static pair => pair.Registration)
            .Concat(model.UnsupportedPairs.Select(static pair =>
                pair.Registration))
            .Concat(model.UnavailablePairs.Select(static pair =>
                pair.Registration))
            .Concat(model.DuplicateRegistrations.Select(static pair =>
                pair.Registration));
    }

    private static bool IsSameInvocation(
        SyntaxNode left,
        SyntaxNode right)
    {
        return left.SyntaxTree == right.SyntaxTree &&
               left.Span == right.Span;
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

    private static string PairKey(MappingPairIdentity identity)
    {
        return identity.Source.Key + "->" + identity.Destination.Key;
    }

    private readonly record struct DiagnosticCandidate(
        int IdOrder,
        string MapperIdentity,
        int LevelOrder,
        string PairKey,
        int Position,
        Diagnostic Diagnostic);
}
