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
        return Build(BuildDeclarations(context, compilationContext));
    }

    public static IncrementalValuesProvider<TypeMapperConfigureInfo> Build(
        IncrementalValuesProvider<MapperDeclarationInfo> mapperDeclarations,
        IncrementalValueProvider<CompilationContext> compilationContext)
    {
        return Build(BuildDeclarations(
            mapperDeclarations,
            compilationContext));
    }

    public static IncrementalValuesProvider<MapperConfigureDeclarationInfo>
        BuildDeclarations(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext)
    {
        return BuildDeclarations(
            MapperDeclarationPipeline.Build(context, compilationContext),
            compilationContext);
    }

    public static IncrementalValuesProvider<MapperConfigureDeclarationInfo>
        BuildDeclarations(
        IncrementalValuesProvider<MapperDeclarationInfo> mapperDeclarations,
        IncrementalValueProvider<CompilationContext> compilationContext)
    {
        return mapperDeclarations
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                TryBuildDeclaration(source, cancellationToken))
            .Where(static declaration => declaration is not null)
            .Select(static (declaration, _) => declaration!)
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildTypeMapperConfigureInfos);
    }

    public static IncrementalValuesProvider<TypeMapperConfigureInfo> Build(
        IncrementalValuesProvider<MapperConfigureDeclarationInfo>
            declarations)
    {
        return declarations
            .Where(static declaration =>
                declaration.State ==
                    MapperConfigureDeclarationState.SourceBody)
            .Select(static (declaration, _) =>
                new TypeMapperConfigureInfo(
                    declaration.Syntax!,
                    declaration.Declaration.MapperType,
                    declaration.Declaration));
    }

    private static MapperConfigureDeclarationInfo? TryBuildDeclaration(
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
            var malformedAttempt = FindMalformedConfigureAttempt(
                mapperType,
                context,
                cancellationToken);

            return new MapperConfigureDeclarationInfo(
                declaration,
                malformedAttempt,
                malformedAttempt is null
                    ? MapperConfigureDeclarationState.Missing
                    : MapperConfigureDeclarationState.CompilerOwnedInvalid);
        }

        MethodDeclarationSyntax? bodylessSyntax = null;

        foreach (var syntaxReference
                 in configureMethod.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!context.SyntaxTrees.Contains(syntaxReference.SyntaxTree) ||
                syntaxReference.GetSyntax(cancellationToken)
                    is not MethodDeclarationSyntax configureSyntax)
            {
                continue;
            }

            if (configureSyntax.Body is not null ||
                configureSyntax.ExpressionBody is not null)
            {
                return new MapperConfigureDeclarationInfo(
                    declaration,
                    configureSyntax,
                    MapperConfigureDeclarationState.SourceBody);
            }

            bodylessSyntax ??= configureSyntax;
        }

        return new MapperConfigureDeclarationInfo(
            declaration,
            bodylessSyntax,
            MapperConfigureDeclarationState.Bodyless);
    }

    private static MethodDeclarationSyntax? FindMalformedConfigureAttempt(
        INamedTypeSymbol mapperType,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        return mapperType.DeclaringSyntaxReferences
            .Where(reference => context.SyntaxTrees.Contains(
                reference.SyntaxTree))
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<ClassDeclarationSyntax>()
            .SelectMany(static declaration => declaration.Members)
            .OfType<MethodDeclarationSyntax>()
            .Where(static method =>
                method.Identifier.ValueText == "Configure" &&
                method.Modifiers.Any(SyntaxKind.OverrideKeyword))
            .Where(method => context.Compilation
                .GetSemanticModel(method.SyntaxTree)
                .GetDiagnostics(method.Span, cancellationToken)
                .Any(static diagnostic =>
                    diagnostic.Severity == DiagnosticSeverity.Error))
            .OrderBy(method =>
                context.SyntaxTrees.GetOrder(method.SyntaxTree))
            .ThenBy(static method => method.SpanStart)
            .FirstOrDefault();
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
