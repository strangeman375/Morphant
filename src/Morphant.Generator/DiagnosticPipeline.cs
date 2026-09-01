using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal static class DiagnosticPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ImmutableArray<Diagnostic>> diagnostics,
        string pipelineName)
    {
        var actualizedDiagnostics = GeneratorStageGuard.Select(
            context,
            diagnostics.Combine(context.CompilationProvider),
            "Actualize" + pipelineName,
            static (source, cancellationToken) =>
                DiagnosticLocationActualizer.Actualize(
                    source.Left,
                    source.Right,
                    cancellationToken),
            ImmutableArray<Diagnostic>.Empty);

        GeneratorStageGuard.RegisterSourceOutput(
            context,
            actualizedDiagnostics,
            "Report" + pipelineName,
            pipelineName,
            static (productionContext, values) =>
            {
                foreach (var diagnostic in values)
                {
                    productionContext.ReportDiagnostic(diagnostic);
                }
            });
    }
}
