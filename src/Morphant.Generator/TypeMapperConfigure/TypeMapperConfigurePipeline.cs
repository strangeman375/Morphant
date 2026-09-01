using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperDeclaration;

namespace Morphant.Generator.TypeMapperConfigure;

internal static class TypeMapperConfigurePipeline
{
    public static IncrementalValuesProvider<TypeMapperConfigureInfo> Build(
        IncrementalGeneratorInitializationContext context)
    {
        return Build(BuildDeclarations(context));
    }

    public static IncrementalValuesProvider<MapperConfigureDeclarationInfo>
        BuildDeclarations(
        IncrementalGeneratorInitializationContext context)
    {
        return BuildDeclarations(
            MapperDeclarationPipeline.Build(context));
    }

    public static IncrementalValuesProvider<MapperConfigureDeclarationInfo>
        BuildDeclarations(
        IncrementalValuesProvider<MapperDeclarationInfo> mapperDeclarations)
    {
        return mapperDeclarations
            .Select(static (declaration, cancellationToken) =>
                TryBuildDeclaration(declaration, cancellationToken))
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
        MapperDeclarationInfo declaration,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var compilation = declaration.Compilation;
        var knownSymbols = KnownSymbols.TryCreate(compilation);

        if (knownSymbols is null)
        {
            return null;
        }

        var syntaxTrees = new SyntaxTreeOrdering(
            compilation.SyntaxTrees);

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
                compilation,
                syntaxTrees,
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

            if (!syntaxTrees.Contains(syntaxReference.SyntaxTree) ||
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
        CSharpCompilation compilation,
        SyntaxTreeOrdering syntaxTrees,
        CancellationToken cancellationToken)
    {
        return mapperType.DeclaringSyntaxReferences
            .Where(reference => syntaxTrees.Contains(
                reference.SyntaxTree))
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<ClassDeclarationSyntax>()
            .SelectMany(static declaration => declaration.Members)
            .OfType<MethodDeclarationSyntax>()
            .Where(static method =>
                method.Identifier.ValueText == "Configure" &&
                method.Modifiers.Any(SyntaxKind.OverrideKeyword))
            .Where(method => compilation
                .GetSemanticModel(method.SyntaxTree)
                .GetDiagnostics(method.Span, cancellationToken)
                .Any(static diagnostic =>
                    diagnostic.Severity == DiagnosticSeverity.Error))
            .OrderBy(method =>
                syntaxTrees.GetOrder(method.SyntaxTree))
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
