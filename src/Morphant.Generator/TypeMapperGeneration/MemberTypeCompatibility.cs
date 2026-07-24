using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MemberTypeCompatibility
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

    public static ImmutableArray<bool> FindCompatibleCandidates(
        ITypeSymbol sourceType,
        INamedTypeSymbol destinationType,
        ImmutableArray<MemberTypeCompatibilityCandidate> candidates,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (candidates.IsEmpty)
        {
            return [];
        }

        var result = new bool[candidates.Length];
        var probeCandidates =
            ImmutableArray.CreateBuilder<ProbeCandidate>();

        for (var index = 0;
             index < candidates.Length;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidate = candidates[index];
            var conversion = compilation.ClassifyConversion(
                candidate.SourceType,
                candidate.DestinationType);

            if (!conversion.IsImplicit ||
                conversion.IsDynamic)
            {
                continue;
            }

            result[index] = true;
            probeCandidates.Add(
                new ProbeCandidate(
                    index,
                    candidate.SourceMemberName,
                    candidate.DestinationMemberName,
                    candidate.CanAssign));
        }

        if (probeCandidates.Count == 0)
        {
            return result.ToImmutableArray();
        }

        var probeTree = BuildProbeTree(
            sourceType,
            destinationType,
            probeCandidates.ToImmutable(),
            mapperType);
        var probeCompilation = compilation
            .WithOptions(
                compilation.Options
                    .WithReportSuppressedDiagnostics(true))
            .AddSyntaxTrees(probeTree);
        var semanticModel =
            probeCompilation.GetSemanticModel(probeTree);
        var assignments = probeTree
            .GetRoot(cancellationToken)
            .DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .ToImmutableArray();

        var diagnostics = semanticModel.GetDiagnostics(
            cancellationToken: cancellationToken);

        for (var index = 0;
             index < probeCandidates.Count;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var assignment = assignments[index];
            var probeConversion = semanticModel.GetConversion(
                assignment.Right,
                cancellationToken);

            if (!probeConversion.IsImplicit ||
                probeConversion.IsDynamic ||
                diagnostics.Any(
                    diagnostic =>
                        NullableConversionDiagnosticIds.Contains(
                            diagnostic.Id) &&
                        diagnostic.Location.SourceSpan
                            .IntersectsWith(assignment.Span)))
            {
                result[probeCandidates[index].CandidateIndex] =
                    false;
            }
        }

        return result.ToImmutableArray();
    }

    private static SyntaxTree BuildProbeTree(
        ITypeSymbol sourceType,
        INamedTypeSymbol destinationType,
        ImmutableArray<ProbeCandidate> candidates,
        INamedTypeSymbol mapperType)
    {
        var writer = new CodeWriter();

        writer.Line("#nullable enable");

        if (!mapperType.ContainingNamespace.IsGlobalNamespace)
        {
            writer.OpenBlock(
                "namespace " +
                mapperType.ContainingNamespace.ToDisplayString());
        }

        var containingTypes = BuildContainingTypes(mapperType);

        foreach (var containingType in containingTypes)
        {
            writer.OpenBlock(
                $"partial {GetDeclarationKind(containingType)} " +
                BuildTypeDeclarationName(containingType));
        }

        writer.OpenBlock(
            "partial class " +
            BuildTypeDeclarationName(mapperType));

        var sourceTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                sourceType);
        var destinationTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                destinationType);

        for (var index = 0;
             index < candidates.Length;
             index++)
        {
            if (index > 0)
            {
                writer.Line();
            }

            var candidate = candidates[index];

            if (candidate.CanAssign)
            {
                writer.Line(
                    $"private static void __MorphantTypeCompatibilityProbe{index}(");
                writer.Indent();
                writer.Line($"{sourceTypeName} source,");
                writer.Line(
                    $"{destinationTypeName} destination)");
                writer.Unindent();
                writer.Line("{");
                writer.Indent();
                writer.Line(
                    $"destination.{Identifier(candidate.DestinationMemberName)} = " +
                    $"source!.{Identifier(candidate.SourceMemberName)};");
                writer.Unindent();
                writer.Line("}");
                continue;
            }

            writer.Line(
                $"private static {destinationTypeName} " +
                $"__MorphantTypeCompatibilityProbe{index}(");
            writer.Indent();
            writer.Line($"{sourceTypeName} source)");
            writer.Unindent();
            writer.Line("{");
            writer.Indent();
            writer.Line($"return new {destinationTypeName}()");
            writer.Line("{");
            writer.Indent();
            writer.Line(
                $"{Identifier(candidate.DestinationMemberName)} = " +
                $"source!.{Identifier(candidate.SourceMemberName)}");
            writer.Unindent();
            writer.Line("};");
            writer.Unindent();
            writer.Line("}");
        }

        writer.CloseBlock();

        for (var index = containingTypes.Length - 1;
             index >= 0;
             index--)
        {
            writer.CloseBlock();
        }

        if (!mapperType.ContainingNamespace.IsGlobalNamespace)
        {
            writer.CloseBlock();
        }

        var parseOptions = mapperType
            .DeclaringSyntaxReferences
            .First()
            .SyntaxTree
            .Options as CSharpParseOptions;

        return CSharpSyntaxTree.ParseText(
            SourceText.From(writer.ToString()),
            parseOptions,
            "Morphant.MemberTypeCompatibilityProbe.g.cs");
    }

    private static ImmutableArray<INamedTypeSymbol>
        BuildContainingTypes(
            INamedTypeSymbol mapperType)
    {
        var result =
            ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        for (var containingType = mapperType.ContainingType;
             containingType is not null;
             containingType = containingType.ContainingType)
        {
            result.Add(containingType);
        }

        return result
            .ToImmutable()
            .Reverse()
            .ToImmutableArray();
    }

    private static string GetDeclarationKind(
        INamedTypeSymbol type)
    {
        if (type.IsRecord)
        {
            return type.TypeKind == TypeKind.Struct
                ? "record struct"
                : "record";
        }

        return type.TypeKind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            _ => throw new InvalidOperationException(
                $"Unsupported containing type kind: {type.TypeKind}.")
        };
    }

    private static string BuildTypeDeclarationName(
        INamedTypeSymbol type)
    {
        var typeName = Identifier(type.Name);

        if (type.TypeParameters.IsEmpty)
        {
            return typeName;
        }

        return
            typeName +
            "<" +
            string.Join(
                ", ",
                type.TypeParameters.Select(
                    static typeParameter =>
                        Identifier(typeParameter.Name))) +
            ">";
    }

    private static string Identifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) !=
                   SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) !=
                   SyntaxKind.None
            ? "@" + value
            : value;
    }

    private readonly record struct ProbeCandidate(
        int CandidateIndex,
        string SourceMemberName,
        string DestinationMemberName,
        bool CanAssign);
}

internal readonly record struct MemberTypeCompatibilityCandidate(
    string SourceMemberName,
    string DestinationMemberName,
    ITypeSymbol SourceType,
    ITypeSymbol DestinationType,
    bool CanAssign);
