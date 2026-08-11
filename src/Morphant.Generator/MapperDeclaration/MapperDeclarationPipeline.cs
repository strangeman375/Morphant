using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.MapperDeclaration;

internal static class MapperDeclarationPipeline
{
    private static readonly ImmutableHashSet<string>
        MalformedBaseDiagnosticIds = ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "CS0060", // Inconsistent base accessibility.
            "CS0104", // Ambiguous type name.
            "CS0146", // Circular base type dependency.
            "CS0246", // Type or namespace not found.
            "CS0305", // Wrong generic arity.
            "CS0308", // Non-generic type used with type arguments.
            "CS0311", // Invalid generic base type argument.
            "CS0314", // Invalid generic base type parameter.
            "CS0400", // Namespace type not found.
            "CS0426", // Nested type not found.
            "CS0433", // Type exists in two assemblies.
            "CS0509", // Cannot derive from sealed type.
            "CS0527", // Base-list type is not an interface.
            "CS0528", // Base-list type is not an interface or class.
            "CS0689", // Cannot derive from a type parameter.
            "CS0701", // Invalid generic base constraint type.
            "CS0713", // Static class cannot derive from type.
            "CS1721", // Multiple base classes.
            "CS1722"); // Base class must precede interfaces.

    public static IncrementalValuesProvider<MapperDeclarationInfo> Build(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext)
    {
        var candidates = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MetadataNames.MorphantMapperAttribute,
                static (node, _) => node is ClassDeclarationSyntax,
                static (attributeContext, cancellationToken) =>
                    CreateCandidate(attributeContext, cancellationToken))
            .Where(static candidate => candidate is not null)
            .Select(static (candidate, _) => candidate!.Value)
            .WithTrackingName(
                MorphantGeneratorStageNames.FindMorphantMapperDeclarations);

        return candidates
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                TryBuild(source.Left, source.Right, cancellationToken))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!)
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildMapperDeclarationInfos);
    }

    private static MapperDeclarationCandidate? CreateCandidate(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var attribute = context.Attributes
            .Select(attributeData =>
                attributeData.ApplicationSyntaxReference?.GetSyntax(
                    cancellationToken))
            .OfType<AttributeSyntax>()
            .OrderBy(static syntax => syntax.SpanStart)
            .FirstOrDefault();

        return attribute is null
            ? null
            : new MapperDeclarationCandidate(
                (ClassDeclarationSyntax)context.TargetNode,
                attribute);
    }

    private static MapperDeclarationInfo? TryBuild(
        MapperDeclarationCandidate candidate,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.KnownSymbols is not { } knownSymbols)
        {
            return null;
        }

        var semanticModel = context.Compilation.GetSemanticModel(
            candidate.Declaration.SyntaxTree);

        if (semanticModel.GetDeclaredSymbol(
                candidate.Declaration,
                cancellationToken) is not INamedTypeSymbol mapperType)
        {
            return null;
        }

        var mapperDeclarations = GetDeclarations<ClassDeclarationSyntax>(
            mapperType,
            context.Compilation,
            cancellationToken);
        var allMapperDeclarationsPartial =
            mapperDeclarations.All(IsPartial);
        var mapperPartialIssue =
            mapperDeclarations.Length == 1 &&
            !allMapperDeclarationsPartial
                ? mapperDeclarations[0]
                : null;
        var containingPartialIssues =
            ImmutableArray.CreateBuilder<MapperContainingTypeIssue>();
        var allContainingDeclarationsPartial = true;
        var fileLocalIssues =
            ImmutableArray.CreateBuilder<MapperContainingTypeIssue>();

        for (var current = mapperType;
             current is not null;
             current = current.ContainingType)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var declarations = GetDeclarations<TypeDeclarationSyntax>(
                current,
                context.Compilation,
                cancellationToken);

            if (current.IsFileLocal &&
                declarations.FirstOrDefault(HasFileModifier) is
                    { } fileLocalDeclaration)
            {
                fileLocalIssues.Add(
                    new MapperContainingTypeIssue(
                        current,
                        fileLocalDeclaration));
            }

            if (SymbolEqualityComparer.Default.Equals(
                    current,
                    mapperType))
            {
                continue;
            }

            var allDeclarationsPartial = declarations.All(IsPartial);
            allContainingDeclarationsPartial &= allDeclarationsPartial;

            if (declarations.Length == 1 &&
                !allDeclarationsPartial)
            {
                containingPartialIssues.Add(
                    new MapperContainingTypeIssue(
                        current,
                        declarations[0]));
            }
        }

        var derivesFromTypeMapper = DerivesFrom(
            mapperType,
            knownSymbols.TypeMapper);

        return new MapperDeclarationInfo(
            candidate.Declaration,
            candidate.Attribute,
            mapperType,
            derivesFromTypeMapper,
            !derivesFromTypeMapper && HasMalformedBaseDeclaration(
                mapperDeclarations,
                context.Compilation,
                cancellationToken),
            mapperPartialIssue,
            allMapperDeclarationsPartial,
            containingPartialIssues.ToImmutable(),
            allContainingDeclarationsPartial,
            fileLocalIssues.ToImmutable(),
            FindConflictingSupportsMethods(
                mapperType,
                knownSymbols.SystemType,
                context.Compilation,
                cancellationToken));
    }

    private static ImmutableArray<TSyntax> GetDeclarations<TSyntax>(
        INamedTypeSymbol type,
        CSharpCompilation compilation,
        CancellationToken cancellationToken)
        where TSyntax : SyntaxNode
    {
        var syntaxTreeOrder = compilation.SyntaxTrees
            .Select((tree, index) => (tree, index))
            .ToDictionary(
                static item => item.tree,
                static item => item.index);

        return type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<TSyntax>()
            .OrderBy(syntax => syntaxTreeOrder[syntax.SyntaxTree])
            .ThenBy(static syntax => syntax.SpanStart)
            .ToImmutableArray();
    }

    private static bool DerivesFrom(
        INamedTypeSymbol mapperType,
        INamedTypeSymbol expectedBaseType)
    {
        for (var current = mapperType.BaseType;
             current is not null;
             current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    current.OriginalDefinition,
                    expectedBaseType.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasMalformedBaseDeclaration(
        ImmutableArray<ClassDeclarationSyntax> declarations,
        CSharpCompilation compilation,
        CancellationToken cancellationToken)
    {
        foreach (var declaration in declarations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (declaration.BaseList is not { } baseList)
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(
                declaration.SyntaxTree);

            if (baseList.Types.Any(baseType =>
                    semanticModel.GetTypeInfo(
                        baseType.Type,
                        cancellationToken).Type is null or IErrorTypeSymbol) ||
                semanticModel.GetDiagnostics(
                        declaration.Span,
                        cancellationToken)
                    .Any(diagnostic =>
                        diagnostic.Severity == DiagnosticSeverity.Error &&
                        MalformedBaseDiagnosticIds.Contains(diagnostic.Id)))
            {
                return true;
            }
        }

        return false;
    }

    private static ImmutableArray<MethodDeclarationSyntax>
        FindConflictingSupportsMethods(
            INamedTypeSymbol mapperType,
            INamedTypeSymbol systemType,
            CSharpCompilation compilation,
            CancellationToken cancellationToken)
    {
        var syntaxTreeOrder = compilation.SyntaxTrees
            .Select((tree, index) => (tree, index))
            .ToDictionary(
                static item => item.tree,
                static item => item.index);

        return mapperType.GetMembers("Supports")
            .OfType<IMethodSymbol>()
            .Where(method => IsConflictingSupports(method, systemType))
            .SelectMany(method => method.DeclaringSyntaxReferences)
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<MethodDeclarationSyntax>()
            .OrderBy(syntax => syntaxTreeOrder[syntax.SyntaxTree])
            .ThenBy(static syntax => syntax.SpanStart)
            .ToImmutableArray();
    }

    private static bool IsConflictingSupports(
        IMethodSymbol method,
        INamedTypeSymbol systemType)
    {
        return method.MethodKind == MethodKind.Ordinary &&
               method.Arity == 0 &&
               method.Parameters.Length == 2 &&
               method.Parameters.All(parameter =>
                   parameter.RefKind == RefKind.None &&
                   SymbolEqualityComparer.Default.Equals(
                       parameter.Type,
                       systemType));
    }

    private static bool IsPartial(TypeDeclarationSyntax declaration)
    {
        return declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
    }

    private static bool HasFileModifier(TypeDeclarationSyntax declaration)
    {
        return declaration.Modifiers.Any(SyntaxKind.FileKeyword);
    }

    private readonly record struct MapperDeclarationCandidate(
        ClassDeclarationSyntax Declaration,
        AttributeSyntax Attribute);
}
