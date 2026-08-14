using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal static class DiagnosticPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<ImmutableArray<Diagnostic>> diagnostics)
    {
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
}
