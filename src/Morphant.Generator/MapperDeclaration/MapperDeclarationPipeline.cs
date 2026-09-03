using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.Compatibility;
using Morphant.Generator.Incrementality;

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
        IncrementalGeneratorInitializationContext context)
    {
        var semanticResults = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                MetadataNames.MorphantMapperAttribute,
                static (node, _) => node is ClassDeclarationSyntax,
                static (attributeContext, cancellationToken) =>
                    GeneratorStageGuard.Execute(
                        attributeContext,
                        MorphantGeneratorStageNames
                            .FindMorphantMapperDeclarations,
                        static (source, token) => TryBuildSemanticInput(
                            source,
                            token),
                        static source => source.TargetNode.GetLocation(),
                        cancellationToken));
        var semanticInputs = GeneratorStageGuard
            .Unwrap(context, semanticResults)
            .Where(static input => input is not null)
            .Select(static (input, _) => input!.Value)
            .WithTrackingName(
                MorphantGeneratorStageNames.FindMorphantMapperDeclarations)
            .WithComparer(MapperSemanticInputComparer.Instance);

        return GeneratorStageGuard
            .Select(
                context,
                semanticInputs,
                MorphantGeneratorStageNames.BuildMapperDeclarationInfos,
                static (input, cancellationToken) =>
                    TryBuild(input, cancellationToken),
                static input =>
                    input.AttributedDeclaration.Identifier.GetLocation())
            .Where(static info => info is not null)
            .Select(static (info, _) => info!)
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildMapperDeclarationInfos);
    }

    private static MapperSemanticInput? TryBuildSemanticInput(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.TargetNode is not
                ClassDeclarationSyntax attributedDeclaration ||
            context.TargetSymbol is not INamedTypeSymbol mapperType ||
            context.SemanticModel.Compilation is not
                CSharpCompilation compilation)
        {
            return null;
        }

        var attribute = context.Attributes
            .Select(attributeData =>
                attributeData.ApplicationSyntaxReference?.GetSyntax(
                    cancellationToken))
            .OfType<AttributeSyntax>()
            .OrderBy(static syntax => syntax.SpanStart)
            .FirstOrDefault();

        if (attribute is null ||
            attributedDeclaration.SyntaxTree.Options is not
                CSharpParseOptions parseOptions)
        {
            return null;
        }

        var compatibility = CompilationCompatibilityDetector.Detect(
            compilation,
            parseOptions.LanguageVersion);

        return compatibility.CanGenerate
            ? new MapperSemanticInput(
                attributedDeclaration,
                attribute,
                mapperType,
                compilation,
                MapperSemanticFingerprintBuilder.Build(
                    attributedDeclaration,
                    attribute,
                    mapperType,
                    compilation,
                    parseOptions.LanguageVersion,
                    compatibility,
                    cancellationToken))
            : null;
    }

    private static MapperDeclarationInfo? TryBuild(
        MapperSemanticInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var compilation = input.Compilation;
        var knownSymbols = KnownSymbols.TryCreate(compilation);

        if (knownSymbols is null)
        {
            return null;
        }

        var mapperType = input.MapperType;
        var syntaxTrees = new SyntaxTreeOrdering(
            compilation.SyntaxTrees);

        var mapperDeclarations = GetDeclarations<ClassDeclarationSyntax>(
            mapperType,
            syntaxTrees,
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
                syntaxTrees,
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

        var typeMapperBase = FindBaseType(
            mapperType,
            knownSymbols.TypeMapper);
        var derivesFromTypeMapper = typeMapperBase is not null;
        var mapperSelfType = typeMapperBase?.TypeArguments[0];
        var invalidSelfTypeLocation =
            mapperSelfType is not null &&
            !IsValidMapperSelfType(mapperType, mapperSelfType)
                ? FindTypeMapperArgumentLocation(
                      mapperType,
                      knownSymbols.TypeMapper,
                      syntaxTrees,
                      compilation,
                      cancellationToken) ??
                  input.AttributedDeclaration.Identifier.GetLocation()
                : null;

        return new MapperDeclarationInfo(
            input.AttributedDeclaration,
            input.Attribute,
            mapperType,
            derivesFromTypeMapper,
            mapperSelfType,
            invalidSelfTypeLocation,
            !derivesFromTypeMapper && HasMalformedBaseDeclaration(
                mapperDeclarations,
                compilation,
                cancellationToken),
            mapperPartialIssue,
            allMapperDeclarationsPartial,
            containingPartialIssues.ToImmutable(),
            allContainingDeclarationsPartial,
            fileLocalIssues.ToImmutable(),
            FindConflictingSupportsMethods(
                mapperType,
                knownSymbols.SystemType,
                syntaxTrees,
                cancellationToken),
            compilation);
    }

    private static bool IsValidMapperSelfType(
        INamedTypeSymbol mapperType,
        ITypeSymbol mapperSelfType)
    {
        if (SymbolEqualityComparer.Default.Equals(
                mapperSelfType,
                mapperType))
        {
            return true;
        }

        return mapperSelfType is ITypeParameterSymbol typeParameter &&
               typeParameter.ConstraintTypes.Any(constraint =>
                   SymbolEqualityComparer.Default.Equals(
                       constraint,
                       mapperType));
    }

    private static ImmutableArray<TSyntax> GetDeclarations<TSyntax>(
        INamedTypeSymbol type,
        SyntaxTreeOrdering syntaxTrees,
        CancellationToken cancellationToken)
        where TSyntax : SyntaxNode
    {
        return type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<TSyntax>()
            .OrderBy(syntax => syntaxTrees.GetOrder(syntax.SyntaxTree))
            .ThenBy(static syntax => syntax.SpanStart)
            .ToImmutableArray();
    }

    private static INamedTypeSymbol? FindBaseType(
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
                return current;
            }
        }

        return null;
    }

    private static Location? FindTypeMapperArgumentLocation(
        INamedTypeSymbol mapperType,
        INamedTypeSymbol typeMapper,
        SyntaxTreeOrdering syntaxTrees,
        CSharpCompilation compilation,
        CancellationToken cancellationToken)
    {
        for (var current = mapperType;
             current is not null;
             current = current.BaseType)
        {
            foreach (var declaration in current.OriginalDefinition
                         .DeclaringSyntaxReferences
                         .Where(reference => syntaxTrees.Contains(
                             reference.SyntaxTree))
                         .Select(reference =>
                             reference.GetSyntax(cancellationToken))
                         .OfType<ClassDeclarationSyntax>())
            {
                if (declaration.BaseList is not { } baseList)
                {
                    continue;
                }

                var semanticModel = compilation.GetSemanticModel(
                    declaration.SyntaxTree);

                foreach (var baseType in baseList.Types)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (semanticModel.GetTypeInfo(
                            baseType.Type,
                            cancellationToken).Type is not
                            INamedTypeSymbol resolved ||
                        !SymbolEqualityComparer.Default.Equals(
                            resolved.OriginalDefinition,
                            typeMapper.OriginalDefinition))
                    {
                        continue;
                    }

                    return baseType.Type
                        .DescendantNodesAndSelf()
                        .OfType<GenericNameSyntax>()
                        .Where(static name =>
                            name.TypeArgumentList.Arguments.Count == 1)
                        .Select(name => new
                        {
                            Name = name,
                            Type = semanticModel.GetTypeInfo(
                                name,
                                cancellationToken).Type
                        })
                        .Where(candidate => candidate.Type is
                            INamedTypeSymbol named &&
                            SymbolEqualityComparer.Default.Equals(
                                named.OriginalDefinition,
                                typeMapper.OriginalDefinition))
                        .Select(static candidate => candidate.Name
                            .TypeArgumentList.Arguments[0].GetLocation())
                        .FirstOrDefault() ?? baseType.Type.GetLocation();
                }
            }
        }

        return null;
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
            SyntaxTreeOrdering syntaxTrees,
            CancellationToken cancellationToken)
    {
        return mapperType.GetMembers("Supports")
            .OfType<IMethodSymbol>()
            .Where(method => IsConflictingSupports(method, systemType))
            .SelectMany(method => method.DeclaringSyntaxReferences)
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<MethodDeclarationSyntax>()
            .OrderBy(syntax => syntaxTrees.GetOrder(syntax.SyntaxTree))
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
}
