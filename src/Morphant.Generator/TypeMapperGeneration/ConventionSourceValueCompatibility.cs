using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ConventionSourceValueCompatibility
{
    public static ImmutableArray<ImmutableArray<ConventionReadableMember>>
        FindCompatibleCandidates(
            ITypeSymbol sourceType,
            ImmutableArray<ConventionSourceValueRequest> requests,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            CancellationToken cancellationToken)
    {
        var results = requests.Select(_ =>
            ImmutableArray.CreateBuilder<ConventionReadableMember>()).ToArray();
        var potential = requests.SelectMany((request, group) => request.Candidates
                .Where(candidate => MappingExpressionCompatibility.HasPotentiallyCompatibleConversion(
                    candidate.Type, request.TargetType, compilation))
                .Select(member => (Group: group, request.TargetType, Member: member)))
            .ToImmutableArray();

        if (potential.IsEmpty)
            return results.Select(result => result.ToImmutable()).ToImmutableArray();

        var sourceTypeName = TypeMapperMappingTypePolicy.GetGeneratedTypeName(sourceType);
        var tree = MapperProbeSyntax.Build(
            mapperType,
            "Morphant.FlatteningCompatibilityProbe.g.cs",
            writer =>
            {
                for (var index = 0; index < potential.Length; index++)
                {
                    if (index > 0)
                    {
                        writer.Line();
                    }

                    var targetTypeName = potential[index].TargetType.ToDisplayString(
                        SymbolDisplayFormats.FullyQualifiedNullable);
                    writer.Line(
                        $"private static {targetTypeName} " +
                        $"__MorphantFlatteningProbe{index}(");
                    writer.Indent();
                    writer.Line($"{sourceTypeName} source)");
                    writer.Unindent();
                    writer.Line("{");
                    writer.Indent();
                    writer.Line(
                        "return " + SourceExpression(
                            potential[index].Member,
                            mapperType) + ";");
                    writer.Unindent();
                    writer.Line("}");
                }
            });
        var probeCompilation = compilation
            .WithOptions(compilation.Options
                .WithReportSuppressedDiagnostics(true))
            .AddSyntaxTrees(tree);
        var semanticModel = probeCompilation.GetSemanticModel(tree);
        var returns = tree.GetRoot(cancellationToken)
            .DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .ToImmutableArray();
        var diagnostics = semanticModel.GetDiagnostics(
            cancellationToken: cancellationToken);
        for (var index = 0; index < potential.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var expression = returns[index].Expression!;
            var conversion = semanticModel.GetConversion(
                expression,
                cancellationToken);

            if (conversion.IsImplicit &&
                !conversion.IsDynamic &&
                !MappingExpressionCompatibility.HasNullableWarning(
                    diagnostics,
                    returns[index].Span))
            {
                results[potential[index].Group].Add(potential[index].Member);
            }
        }

        return results.Select(result => result.ToImmutable()).ToImmutableArray();
    }

    private static string SourceExpression(
        ConventionReadableMember member,
        INamedTypeSymbol mapperType)
    {
        var localNames = new GeneratedLocalNameAllocator(
            mapperType,
            "source");

        return member.BuildConventionValueExpression("source!")
                   ?.Render(localNames) ??
               "source!." + Identifier(member.Name);
    }

    private static string Identifier(string value) =>
        SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
        SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value
            : value;
}

internal readonly record struct ConventionSourceValueRequest(
    ITypeSymbol TargetType,
    ImmutableArray<ConventionReadableMember> Candidates);
