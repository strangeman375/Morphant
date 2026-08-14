using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TypeMapperTransferValidator
{
    private const string UnsupportedTransferMessage =
        "This mapping contains code that Morphant cannot generate.";

    public static TypeMapperTransferValidationResult Validate(
        TypeMapperModel model,
        ImmutableArray<TransferredCodePolicy> policies,
        CSharpCompilation compilation,
        CSharpParseOptions? parseOptions,
        CancellationToken cancellationToken)
    {
        if (!policies.Any(static policy => policy.HasTransferredCode))
        {
            return new TypeMapperTransferValidationResult(
                model,
                ImmutableArray<CallbackTransferFailureObservation>.Empty);
        }

        var transferFailures = ImmutableArray.CreateBuilder<
            CallbackTransferFailureObservation>();
        var seenTransferFailures = new HashSet<string>(StringComparer.Ordinal);

        var diagnostics = GetDiagnostics(
            model,
            compilation,
            parseOptions,
            cancellationToken);

        if (diagnostics.IsEmpty)
        {
            return new TypeMapperTransferValidationResult(
                model,
                ImmutableArray<CallbackTransferFailureObservation>.Empty);
        }

        var mappings = model.Mappings.ToArray();
        var suppressions = new HashSet<string>?[mappings.Length];
        var failures = new MappingFailureObservation?[mappings.Length];
        var unmappedDiagnostics =
            ImmutableArray.CreateBuilder<TransferPreflightDiagnostic>();

        foreach (var preflightDiagnostic in diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var diagnostic = preflightDiagnostic.Diagnostic;

            if (!TypeMapperEmitter.TryGetTransferProbeMappingIndex(
                    diagnostic,
                    out var mappingIndex) ||
                mappingIndex < 0 ||
                mappingIndex >= policies.Length ||
                !policies[mappingIndex].HasTransferredCode)
            {
                unmappedDiagnostics.Add(preflightDiagnostic);
                continue;
            }

            if (diagnostic.DefaultSeverity ==
                    DiagnosticSeverity.Warning &&
                (policies[mappingIndex].IsSourceOwned(
                     diagnostic,
                     cancellationToken) ||
                 policies[mappingIndex].CanSuppress(
                     diagnostic,
                     cancellationToken)))
            {
                (suppressions[mappingIndex] ??=
                    new HashSet<string>(StringComparer.Ordinal))
                    .Add(diagnostic.Id);
            }
            else
            {
                failures[mappingIndex] = BuildFailure(
                    mappings[mappingIndex],
                    policies[mappingIndex],
                    preflightDiagnostic);
                AddTransferFailure(
                    policies[mappingIndex],
                    diagnostic.Id,
                    transferFailures,
                    seenTransferFailures);
            }
        }

        if (unmappedDiagnostics.Count > 0)
        {
            for (var index = 0; index < policies.Length; index++)
            {
                if (policies[index].HasTransferredCode)
                {
                    failures[index] ??= BuildFailure(
                        mappings[index],
                        policies[index],
                        unmappedDiagnostics[0]);
                    AddTransferFailure(
                        policies[index],
                        unmappedDiagnostics[0].Diagnostic.Id,
                        transferFailures,
                        seenTransferFailures);
                }
            }
        }

        ApplyDecisions(
            mappings,
            suppressions,
            failures);
        model = model with
        {
            Mappings = mappings.ToImmutableArray()
        };

        diagnostics = GetDiagnostics(
            model,
            compilation,
            parseOptions,
            cancellationToken);

        if (diagnostics.IsEmpty)
        {
            return new TypeMapperTransferValidationResult(
                model,
                transferFailures.ToImmutable());
        }

        foreach (var preflightDiagnostic in diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var diagnostic = preflightDiagnostic.Diagnostic;

            if (TypeMapperEmitter.TryGetTransferProbeMappingIndex(
                    diagnostic,
                    out var mappingIndex) &&
                mappingIndex >= 0 &&
                mappingIndex < policies.Length &&
                policies[mappingIndex].HasTransferredCode)
            {
                mappings[mappingIndex] = MakeUnsupported(
                    mappings[mappingIndex],
                    BuildFailure(
                        mappings[mappingIndex],
                        policies[mappingIndex],
                        preflightDiagnostic));
                AddTransferFailure(
                    policies[mappingIndex],
                    diagnostic.Id,
                    transferFailures,
                    seenTransferFailures);
                continue;
            }

            for (var index = 0; index < policies.Length; index++)
            {
                if (policies[index].HasTransferredCode)
                {
                    mappings[index] = MakeUnsupported(
                        mappings[index],
                        BuildFailure(
                            mappings[index],
                            policies[index],
                            preflightDiagnostic));
                    AddTransferFailure(
                        policies[index],
                        diagnostic.Id,
                        transferFailures,
                        seenTransferFailures);
                }
            }
        }

        return new TypeMapperTransferValidationResult(
            model with
            {
                Mappings = mappings.ToImmutableArray()
            },
            transferFailures.ToImmutable());
    }

    private static void AddTransferFailure(
        TransferredCodePolicy policy,
        string diagnosticId,
        ImmutableArray<CallbackTransferFailureObservation>.Builder result,
        ISet<string> seen)
    {
        if (policy.PrimaryExpression is not { } expression)
        {
            return;
        }

        var key = expression.Syntax.SyntaxTree.FilePath + "|" +
                  expression.Syntax.SpanStart + "|" +
                  expression.Syntax.Span.Length + "|" + diagnosticId;

        if (seen.Add(key))
        {
            result.Add(new CallbackTransferFailureObservation(
                expression,
                diagnosticId));
        }
    }

    private static void ApplyDecisions(
        TypeMapperMappingModel[] mappings,
        IReadOnlyList<HashSet<string>?> suppressions,
        IReadOnlyList<MappingFailureObservation?> failures)
    {
        for (var index = 0; index < mappings.Length; index++)
        {
            if (failures[index] is { } failure)
            {
                mappings[index] = MakeUnsupported(
                    mappings[index],
                    failure);
                continue;
            }

            if (suppressions[index] is not { Count: > 0 } warningIds)
            {
                continue;
            }

            mappings[index] = mappings[index] with
            {
                TransferredWarningSuppressions = warningIds
                    .OrderBy(static id => id, StringComparer.Ordinal)
                    .ToImmutableArray()
            };
        }
    }

    private static TypeMapperMappingModel MakeUnsupported(
        TypeMapperMappingModel mapping,
        MappingFailureObservation failure)
    {
        var completeness = mapping.CompletenessObservation;

        if (completeness is not null)
        {
            var uncertainty = completeness.ErrorDerivedUncertainty
                .ToBuilder();

            foreach (var member in completeness.SupportedSourceMembers
                         .AddRange(
                             completeness.SupportedDestinationMembers))
            {
                if (!uncertainty.Any(candidate =>
                        SymbolEqualityComparer.Default.Equals(
                            candidate,
                            member)))
                {
                    uncertainty.Add(member);
                }
            }

            completeness = completeness with
            {
                ErrorDerivedUncertainty = uncertainty.ToImmutable()
            };
        }

        return mapping with
        {
            Failure = failure,
            CompletenessObservation = completeness,
            CreateImplMethodName = null,
            UpdateImplMethodName = null,
            CreateImplUsesOperation = false,
            HelperMethodDeclarations = ImmutableArray<string>.Empty,
            TransferredWarningSuppressions = ImmutableArray<string>.Empty
        };
    }

    private static MappingFailureObservation BuildFailure(
        TypeMapperMappingModel mapping,
        TransferredCodePolicy policy,
        TransferPreflightDiagnostic diagnostic)
    {
        var sourceExpression = policy.PrimaryExpression;

        return MappingFailureObservation.Create(
            mapping.AnalysisContext,
            MappingFailureReason.CallbackCannotBeTransferred,
            UnsupportedTransferMessage,
            MappingObservationOriginKind.CompilerPreflight,
            MappingAffectedPath.All(MappingPlanPhase.Transfer),
            sourceExpression?.Syntax,
            sourceExpression?.DeclaringMapperType,
            diagnostic.Node,
            diagnostic.Symbol,
            sourceExpression?.Syntax.GetLocation(),
            ImmutableArray.Create<Location>(diagnostic.Diagnostic.Location));
    }

    private static ImmutableArray<TransferPreflightDiagnostic> GetDiagnostics(
        TypeMapperModel model,
        CSharpCompilation compilation,
        CSharpParseOptions? parseOptions,
        CancellationToken cancellationToken)
    {
        var source = TypeMapperEmitter.EmitTransferProbe(model);
        var syntaxTree = CSharpSyntaxTree.ParseText(
            SourceText.From(source.ToString(), Encoding.UTF8),
            parseOptions,
            "Morphant.TransferProbe.g.cs",
            cancellationToken);
        var probeCompilation = compilation.AddSyntaxTrees(syntaxTree);
        var semanticModel = probeCompilation.GetSemanticModel(syntaxTree);

        return semanticModel.GetDiagnostics(
                cancellationToken: cancellationToken)
            .Where(diagnostic =>
                ReferenceEquals(
                    diagnostic.Location.SourceTree,
                    syntaxTree) &&
                diagnostic.Severity is
                    DiagnosticSeverity.Warning or
                    DiagnosticSeverity.Error)
            .Select(diagnostic =>
            {
                var node = syntaxTree.GetRoot(cancellationToken)
                    .FindNode(
                        diagnostic.Location.SourceSpan,
                        getInnermostNodeForTie: true);

                return new TransferPreflightDiagnostic(
                    diagnostic,
                    node,
                    semanticModel.GetSymbolInfo(
                            node,
                            cancellationToken)
                        .Symbol);
            })
            .ToImmutableArray();
    }

    private sealed record TransferPreflightDiagnostic(
        Diagnostic Diagnostic,
        SyntaxNode Node,
        ISymbol? Symbol);
}

internal readonly record struct TypeMapperTransferValidationResult(
    TypeMapperModel Model,
    ImmutableArray<CallbackTransferFailureObservation> Failures);

internal sealed record CallbackTransferFailureObservation(
    BoundConfigurationExpression Expression,
    string DiagnosticId);
