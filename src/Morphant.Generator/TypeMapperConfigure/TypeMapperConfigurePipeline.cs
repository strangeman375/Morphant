using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperConfigure;

internal static class TypeMapperConfigurePipeline
{
    public static IncrementalValuesProvider<TypeMapperConfigureInfo> Build(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext)
    {
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(IsCandidate, static (syntaxContext, _) => (MethodDeclarationSyntax)syntaxContext.Node)
            .WithTrackingName(MorphantGeneratorStageNames.FindTypeMapperConfigureCandidates);

        return candidates
            .Combine(compilationContext)
            .Select(TryBuild)
            .WhereHasValue()
            .WithTrackingName(MorphantGeneratorStageNames.BuildTypeMapperConfigureInfos);
    }

    private static bool IsCandidate(SyntaxNode node, CancellationToken _) =>
        node is MethodDeclarationSyntax method
        && method.Identifier.ValueText == "Configure"
        && method.ParameterList.Parameters.Count == 1
        && method.TypeParameterList is null
        && method.ReturnType is PredefinedTypeSyntax returnType
        && returnType.Keyword.IsKind(SyntaxKind.VoidKeyword)
        && method.Modifiers.Any(SyntaxKind.OverrideKeyword)
        && (method.Body is not null || method.ExpressionBody is not null);

    private static TypeMapperConfigureInfo? TryBuild(
        (MethodDeclarationSyntax candidate, CompilationContext context) source,
        CancellationToken cancellationToken)
    {
        var (candidate, context) = source;

        var semanticModel = context.Compilation.GetSemanticModel(candidate.SyntaxTree);
        var method = semanticModel.GetDeclaredSymbol(candidate, cancellationToken);

        if (method is null
            || !IsTypeMapperConfigureOverride(method, context.KnownSymbols))
        {
            return null;
        }

        return new TypeMapperConfigureInfo(candidate, method);
    }

    private static bool IsTypeMapperConfigureOverride(
        IMethodSymbol method,
        KnownSymbols knownSymbols)
    {
        var parameter = method.Parameters[0];
        if (!SymbolEqualityComparer.Default.Equals(parameter.Type, knownSymbols.MapperBuilder))
        {
            return false;
        }

        var current = method;
        while ((current = current.OverriddenMethod) is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, knownSymbols.TypeMapperConfigure.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }
}
