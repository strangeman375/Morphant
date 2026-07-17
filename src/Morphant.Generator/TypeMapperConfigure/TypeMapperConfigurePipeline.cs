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
        var mapperDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MetadataNames.MorphantMapperAttribute,
                static (node, _) =>
                    node is ClassDeclarationSyntax,
                static (attributeContext, _) =>
                    (ClassDeclarationSyntax)attributeContext.TargetNode)
            .WithTrackingName(
                MorphantGeneratorStageNames.FindMorphantMapperDeclarations);

        return mapperDeclarations
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                TryBuild(source, cancellationToken))
            .WhereHasValue()
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildTypeMapperConfigureInfos);
    }

    private static TypeMapperConfigureInfo? TryBuild(
        (
            ClassDeclarationSyntax MapperDeclaration,
            CompilationContext Context
        ) source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (mapperDeclaration, context) = source;

        if (context.KnownSymbols is not { } knownSymbols)
        {
            return null;
        }

        var semanticModel = context.Compilation.GetSemanticModel(
            mapperDeclaration.SyntaxTree);

        if (semanticModel.GetDeclaredSymbol(
                mapperDeclaration,
                cancellationToken) is not INamedTypeSymbol mapperType)
        {
            return null;
        }

        var configureMethod = FindConfigureOverride(
            mapperType,
            knownSymbols,
            cancellationToken);

        if (configureMethod is null)
        {
            return null;
        }

        foreach (var syntaxReference
                 in configureMethod.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (syntaxReference.GetSyntax(cancellationToken)
                    is MethodDeclarationSyntax
                    {
                        Body: not null
                    } configureSyntax)
            {
                return new TypeMapperConfigureInfo(
                    configureSyntax);
            }

            if (syntaxReference.GetSyntax(cancellationToken)
                    is MethodDeclarationSyntax
                    {
                        ExpressionBody: not null
                    } expressionBodiedSyntax)
            {
                return new TypeMapperConfigureInfo(
                    expressionBodiedSyntax);
            }
        }

        return null;
    }

    private static IMethodSymbol? FindConfigureOverride(
        INamedTypeSymbol mapperType,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        foreach (var method in mapperType
                     .GetMembers("Configure")
                     .OfType<IMethodSymbol>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsTypeMapperConfigureOverride(
                    method,
                    knownSymbols))
            {
                return method;
            }
        }

        return null;
    }

    private static bool IsTypeMapperConfigureOverride(
        IMethodSymbol method,
        KnownSymbols knownSymbols)
    {
        if (!method.IsOverride ||
            method.IsStatic ||
            !method.ReturnsVoid ||
            method.TypeParameters.Length != 0 ||
            method.Parameters.Length != 1 ||
            !SymbolEqualityComparer.Default.Equals(
                method.Parameters[0].Type,
                knownSymbols.MapperBuilder))
        {
            return false;
        }

        for (var overridden = method.OverriddenMethod;
             overridden is not null;
             overridden = overridden.OverriddenMethod)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    overridden.OriginalDefinition,
                    knownSymbols.TypeMapperConfigure.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }
}
