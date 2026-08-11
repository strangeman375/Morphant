using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperDeclaration;

namespace Morphant.Generator.TypeMapperConfigure;

internal static class TypeMapperConfigurePipeline
{
    public static IncrementalValuesProvider<TypeMapperConfigureInfo> Build(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext)
    {
        return Build(
            MapperDeclarationPipeline.Build(context, compilationContext),
            compilationContext);
    }

    public static IncrementalValuesProvider<TypeMapperConfigureInfo> Build(
        IncrementalValuesProvider<MapperDeclarationInfo> mapperDeclarations,
        IncrementalValueProvider<CompilationContext> compilationContext)
    {
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
            MapperDeclarationInfo Declaration,
            CompilationContext Context
        ) source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (declaration, context) = source;

        if (context.KnownSymbols is not { } knownSymbols)
        {
            return null;
        }

        if (!declaration.DerivesFromTypeMapper)
        {
            return null;
        }

        var mapperType = declaration.MapperType;

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
                    configureSyntax,
                    mapperType,
                    declaration);
            }

            if (syntaxReference.GetSyntax(cancellationToken)
                    is MethodDeclarationSyntax
                    {
                        ExpressionBody: not null
                    } expressionBodiedSyntax)
            {
                return new TypeMapperConfigureInfo(
                    expressionBodiedSyntax,
                    mapperType,
                    declaration);
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
