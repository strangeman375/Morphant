using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ConventionSourceValueCompatibility
{
    public static ImmutableArray<ConventionReadableMember>
        FindCompatibleCandidates(
            ITypeSymbol sourceType,
            ITypeSymbol targetType,
            ImmutableArray<ConventionReadableMember> candidates,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            CancellationToken cancellationToken)
    {
        var potential = candidates.Where(candidate =>
                MappingExpressionCompatibility
                    .HasPotentiallyCompatibleConversion(
                        candidate.Type,
                        targetType,
                        compilation))
            .ToImmutableArray();

        if (potential.IsEmpty)
        {
            return potential;
        }

        var sourceTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(sourceType);
        var targetTypeName = targetType.ToDisplayString(
            SymbolDisplayFormats.FullyQualifiedNullable);
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
                            potential[index],
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
        var result =
            ImmutableArray.CreateBuilder<ConventionReadableMember>();

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
                result.Add(potential[index]);
            }
        }

        return result.ToImmutable();
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
