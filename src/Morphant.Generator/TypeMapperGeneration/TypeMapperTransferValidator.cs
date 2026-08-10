using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TypeMapperTransferValidator
{
    private const string UnsupportedTransferMessage =
        "The configured mapping contains code that cannot be transferred " +
        "into the generated mapper.";

    public static TypeMapperModel Validate(
        TypeMapperModel model,
        ImmutableArray<TransferredCodePolicy> policies,
        CSharpCompilation compilation,
        CSharpParseOptions? parseOptions,
        CancellationToken cancellationToken)
    {
        if (!policies.Any(static policy => policy.HasTransferredCode))
        {
            return model;
        }

        var diagnostics = GetDiagnostics(
            model,
            compilation,
            parseOptions,
            cancellationToken);

        if (diagnostics.IsEmpty)
        {
            return model;
        }

        var mappings = model.Mappings.ToArray();
        var suppressions = new HashSet<string>?[mappings.Length];
        var unsupported = new bool[mappings.Length];
        var hasUnmappedDiagnostic = false;

        foreach (var diagnostic in diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TypeMapperEmitter.TryGetTransferProbeMappingIndex(
                    diagnostic,
                    out var mappingIndex) ||
                mappingIndex < 0 ||
                mappingIndex >= policies.Length ||
                !policies[mappingIndex].HasTransferredCode)
            {
                hasUnmappedDiagnostic = true;
                continue;
            }

            if (policies[mappingIndex].CanSuppress(
                    diagnostic,
                    cancellationToken))
            {
                (suppressions[mappingIndex] ??=
                    new HashSet<string>(StringComparer.Ordinal))
                    .Add(diagnostic.Id);
            }
            else
            {
                unsupported[mappingIndex] = true;
            }
        }

        if (hasUnmappedDiagnostic)
        {
            for (var index = 0; index < policies.Length; index++)
            {
                if (policies[index].HasTransferredCode)
                {
                    unsupported[index] = true;
                }
            }
        }

        ApplyDecisions(
            mappings,
            suppressions,
            unsupported);
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
            return model;
        }

        foreach (var diagnostic in diagnostics)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TypeMapperEmitter.TryGetTransferProbeMappingIndex(
                    diagnostic,
                    out var mappingIndex) &&
                mappingIndex >= 0 &&
                mappingIndex < policies.Length &&
                policies[mappingIndex].HasTransferredCode)
            {
                mappings[mappingIndex] = MakeUnsupported(
                    mappings[mappingIndex]);
                continue;
            }

            for (var index = 0; index < policies.Length; index++)
            {
                if (policies[index].HasTransferredCode)
                {
                    mappings[index] = MakeUnsupported(mappings[index]);
                }
            }
        }

        return model with
        {
            Mappings = mappings.ToImmutableArray()
        };
    }

    private static void ApplyDecisions(
        TypeMapperMappingModel[] mappings,
        IReadOnlyList<HashSet<string>?> suppressions,
        IReadOnlyList<bool> unsupported)
    {
        for (var index = 0; index < mappings.Length; index++)
        {
            if (unsupported[index])
            {
                mappings[index] = MakeUnsupported(mappings[index]);
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
        TypeMapperMappingModel mapping)
    {
        return mapping with
        {
            UnsupportedExceptionMessage = UnsupportedTransferMessage,
            CreateImplMethodName = null,
            UpdateImplMethodName = null,
            CreateImplUsesOperation = false,
            HelperMethodDeclarations = [],
            TransferredWarningSuppressions = []
        };
    }

    private static ImmutableArray<Diagnostic> GetDiagnostics(
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
            .ToImmutableArray();
    }
}
