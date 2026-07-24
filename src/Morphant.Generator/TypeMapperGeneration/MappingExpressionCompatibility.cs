using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MappingExpressionCompatibility
{
    private static readonly HashSet<string>
        NullableConversionDiagnosticIds =
        [
            "CS8600",
            "CS8601",
            "CS8602",
            "CS8603",
            "CS8604",
            "CS8605",
            "CS8607",
            "CS8608",
            "CS8609",
            "CS8610",
            "CS8611",
            "CS8612",
            "CS8613",
            "CS8614",
            "CS8615",
            "CS8616",
            "CS8617",
            "CS8618",
            "CS8619",
            "CS8620",
            "CS8621",
            "CS8622",
            "CS8624",
            "CS8625",
            "CS8629",
            "CS8631",
            "CS8632",
            "CS8633",
            "CS8634",
            "CS8643",
            "CS8644",
            "CS8645",
            "CS8655",
            "CS8667",
            "CS8669",
            "CS8670",
            "CS8714",
            "CS8762",
            "CS8764",
            "CS8765",
            "CS8766",
            "CS8767",
            "CS8768",
            "CS8769",
            "CS8774",
            "CS8775",
            "CS8776",
            "CS8777",
            "CS8819",
            "CS8824",
            "CS8825",
            "CS8847",
            "CS9158",
            "CS9159",
            "CS9264"
        ];

    public static bool HasPotentiallyCompatibleConversion(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        CSharpCompilation compilation)
    {
        var conversion = compilation.ClassifyConversion(
            sourceType,
            destinationType);

        return conversion.IsImplicit &&
               !conversion.IsDynamic;
    }

    public static bool HasNullableWarning(
        IEnumerable<Diagnostic> diagnostics,
        TextSpan span)
    {
        return diagnostics.Any(
            diagnostic =>
                NullableConversionDiagnosticIds.Contains(
                    diagnostic.Id) &&
                diagnostic.Location.SourceSpan
                    .IntersectsWith(span));
    }
}
