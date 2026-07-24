using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MemberTypeCompatibility
{
    public static ImmutableArray<bool> FindCompatibleCandidates(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
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
            if (!MappingExpressionCompatibility
                    .HasPotentiallyCompatibleConversion(
                        candidate.SourceType,
                        candidate.DestinationType,
                        compilation))
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
                MappingExpressionCompatibility.HasNullableWarning(
                    diagnostics,
                    assignment.Span))
            {
                result[probeCandidates[index].CandidateIndex] =
                    false;
            }
        }

        return result.ToImmutableArray();
    }

    private static SyntaxTree BuildProbeTree(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        ImmutableArray<ProbeCandidate> candidates,
        INamedTypeSymbol mapperType)
    {
        var sourceTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                sourceType);
        var destinationTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                destinationType);

        return MapperProbeSyntax.Build(
            mapperType,
            "Morphant.MemberTypeCompatibilityProbe.g.cs",
            writer =>
            {
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
                    writer.Line(
                        $"return new {destinationTypeName}()");
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
            });
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
