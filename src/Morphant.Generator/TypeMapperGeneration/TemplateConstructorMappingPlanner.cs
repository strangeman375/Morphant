using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TemplateConstructorMappingPlanner
{
    private const string ConstructorMemberMetadataName =
        "Morphant.Members.ConstructorMember`1";

    public static TemplateConstructorMappingPlan? Build(
        ImplicitObjectCreationExpressionSyntax objectCreation,
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        SemanticModel templateSemanticModel,
        Func<ExpressionSyntax, string> rewriteExpression,
        CancellationToken cancellationToken)
    {
        if (destination.TypeKind == TypeKind.Interface ||
            destination.IsAbstract ||
            objectCreation.ArgumentList.Arguments.Any(
                static argument =>
                    !argument.RefKindKeyword.IsKind(SyntaxKind.None)))
        {
            return null;
        }

        var constructors = BuildCandidateConstructors(
            destination,
            compilation,
            cancellationToken);

        if (constructors.IsEmpty)
        {
            return null;
        }

        var usedNames = BuildUsedProbeNames(mapperType);
        var probeTypeName = MakeUnique(
            "__MorphantTemplateConstructorProbe",
            usedNames);
        var probeMethodName = MakeUnique(
            "__MorphantBindTemplateConstructor",
            usedNames);
        var destinationProbeMethodName = MakeUnique(
            "__MorphantBindDestinationConstructor",
            usedNames);
        var probeTree = BuildProbeTree(
            sourceType,
            constructors,
            objectCreation.ArgumentList.Arguments,
            mapperType,
            probeTypeName,
            probeMethodName,
            expression => BuildProbeArgumentExpression(
                expression,
                compilation,
                templateSemanticModel,
                rewriteExpression,
                cancellationToken));
        var probeCompilation = compilation
            .WithOptions(
                compilation.Options
                    .WithReportSuppressedDiagnostics(true))
            .AddSyntaxTrees(probeTree);
        var probeSemanticModel =
            probeCompilation.GetSemanticModel(probeTree);
        var probeRoot = probeTree.GetRoot(cancellationToken);
        var probeType = probeRoot
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single(type =>
                type.Identifier.ValueText == probeTypeName);
        var probeMethod = probeRoot
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method =>
                method.Identifier.ValueText == probeMethodName);
        var probeObjectCreation = probeMethod
            .DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .First();
        var selectedProbeConstructor = probeSemanticModel
            .GetSymbolInfo(
                probeObjectCreation,
                cancellationToken)
            .Symbol as IMethodSymbol;

        if (selectedProbeConstructor?.DeclaringSyntaxReferences
                .FirstOrDefault()?
                .GetSyntax(cancellationToken) is not
            ConstructorDeclarationSyntax selectedSyntax)
        {
            return null;
        }

        var probeConstructors = probeType.Members
            .OfType<ConstructorDeclarationSyntax>()
            .ToImmutableArray();
        var selectedConstructorIndex = -1;

        for (var index = 0;
             index < probeConstructors.Length;
             index++)
        {
            if (probeConstructors[index].SpanStart ==
                selectedSyntax.SpanStart)
            {
                selectedConstructorIndex = index;
                break;
            }
        }

        if (selectedConstructorIndex < 0 ||
            selectedConstructorIndex >= constructors.Length)
        {
            return null;
        }

        var destinationConstructor =
            constructors[selectedConstructorIndex];
        var probeArgumentList =
            probeObjectCreation.ArgumentList;

        if (probeArgumentList is null ||
            probeArgumentList.Arguments.Count !=
                objectCreation.ArgumentList.Arguments.Count)
        {
            return null;
        }

        var arguments =
            ImmutableArray.CreateBuilder<
                TypeMapperConstructorArgumentMappingModel>(
                objectCreation.ArgumentList.Arguments.Count);

        for (var index = 0;
             index < probeArgumentList.Arguments.Count;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var probeArgument =
                probeArgumentList.Arguments[index];

            if (probeSemanticModel.GetOperation(
                    probeArgument,
                    cancellationToken) is not IArgumentOperation
                {
                    Parameter: { } probeParameter
                } ||
                probeParameter.Ordinal < 0 ||
                probeParameter.Ordinal >=
                    destinationConstructor.Parameters.Length)
            {
                return null;
            }

            var destinationParameter =
                destinationConstructor.Parameters[
                    probeParameter.Ordinal];
            var templateArgument =
                objectCreation.ArgumentList.Arguments[index];

            arguments.Add(
                new TypeMapperConstructorArgumentMappingModel(
                    destinationParameter.Name,
                    SourceMemberName: string.Empty,
                    ValueLocalName: null,
                    BuildArgumentExpression(
                        templateArgument.Expression,
                        destinationParameter,
                        compilation,
                        templateSemanticModel,
                        rewriteExpression,
                        cancellationToken)));
        }

        var argumentModels = arguments.ToImmutable();

        if (!BindsDestinationConstructor(
                sourceType,
                destination,
                destinationConstructor,
                argumentModels,
                compilation,
                mapperType,
                destinationProbeMethodName,
                cancellationToken))
        {
            return null;
        }

        return new TemplateConstructorMappingPlan(
            destinationConstructor,
            argumentModels);
    }

    private static ImmutableArray<IMethodSymbol>
        BuildCandidateConstructors(
            INamedTypeSymbol destination,
            CSharpCompilation compilation,
            CancellationToken cancellationToken)
    {
        var result =
            ImmutableArray.CreateBuilder<IMethodSymbol>();

        foreach (var constructor in destination.InstanceConstructors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (compilation.IsSymbolAccessibleWithin(
                    constructor,
                    compilation.Assembly) &&
                !constructor.Parameters.Any(
                    static parameter =>
                        parameter.RefKind != RefKind.None ||
                        parameter.Type.IsRefLikeType))
            {
                result.Add(constructor);
            }
        }

        return result.ToImmutable();
    }

    private static SyntaxTree BuildProbeTree(
        ITypeSymbol sourceType,
        ImmutableArray<IMethodSymbol> constructors,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        INamedTypeSymbol mapperType,
        string probeTypeName,
        string probeMethodName,
        Func<ExpressionSyntax, string> rewriteExpression)
    {
        var sourceTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                sourceType);

        return MapperProbeSyntax.Build(
            mapperType,
            "Morphant.TemplateConstructorProbe.g.cs",
            writer =>
            {
                writer.OpenBlock(
                    $"private sealed class {probeTypeName}");

                foreach (var constructor in constructors)
                {
                    var parameters = constructor.Parameters
                        .Select(parameter =>
                            BuildProbeParameter(parameter))
                        .ToArray();

                    writer.Line(
                        $"public {probeTypeName}(" +
                        string.Join(", ", parameters) +
                        ") { }");
                }

                writer.CloseBlock();
                writer.Line();
                writer.Line(
                    $"private {probeTypeName} " +
                    $"{probeMethodName}({sourceTypeName} source)");
                writer.Line("{");
                writer.Indent();

                if (arguments.Count == 0)
                {
                    writer.Line(
                        $"return new {probeTypeName}();");
                }
                else
                {
                    writer.Line(
                        $"return new {probeTypeName}(");
                    writer.Indent();

                    for (var index = 0;
                         index < arguments.Count;
                         index++)
                    {
                        var argument = arguments[index];
                        var prefix = argument.NameColon is { } nameColon
                            ? nameColon.Name.Identifier.Text + ": "
                            : string.Empty;
                        var suffix = index < arguments.Count - 1
                            ? ","
                            : ");";

                        writer.Line(
                            prefix +
                            rewriteExpression(argument.Expression) +
                            suffix);
                    }

                    writer.Unindent();
                }

                writer.Unindent();
                writer.Line("}");
            });
    }

    private static string BuildProbeParameter(
        IParameterSymbol parameter)
    {
        var parameterType = parameter.Type
            .WithNullableAnnotation(
                parameter.NullableAnnotation)
            .ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable);
        var optionalSuffix =
            parameter.IsOptional || parameter.IsParams
                ? " = null!"
                : string.Empty;

        return
            $"global::Morphant.Members.ConstructorMember<{parameterType}> " +
            Identifier(parameter.Name) +
            optionalSuffix;
    }

    private static bool BindsDestinationConstructor(
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        IMethodSymbol selectedConstructor,
        ImmutableArray<TypeMapperConstructorArgumentMappingModel> arguments,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        string probeMethodName,
        CancellationToken cancellationToken)
    {
        var sourceTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                sourceType);
        var destinationTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                destination);
        var probeTree = MapperProbeSyntax.Build(
            mapperType,
            "Morphant.TemplateDestinationConstructorProbe.g.cs",
            writer =>
            {
                writer.Line(
                    $"private {destinationTypeName} " +
                    $"{probeMethodName}({sourceTypeName} source)");
                writer.Line("{");
                writer.Indent();

                if (arguments.IsEmpty)
                {
                    writer.Line(
                        $"return new {destinationTypeName}();");
                }
                else
                {
                    writer.Line(
                        $"return new {destinationTypeName}(");
                    writer.Indent();

                    for (var index = 0;
                         index < arguments.Length;
                         index++)
                    {
                        var argument = arguments[index];
                        var suffix = index < arguments.Length - 1
                            ? ","
                            : ");";

                        writer.Line(
                            $"{Identifier(argument.ParameterName)}: " +
                            (argument.ExplicitValueExpression ??
                             throw new InvalidOperationException(
                                 "Template constructor arguments require an explicit value.")) +
                            suffix);
                    }

                    writer.Unindent();
                }

                writer.Unindent();
                writer.Line("}");
            });
        var probeCompilation = compilation
            .WithOptions(
                compilation.Options
                    .WithReportSuppressedDiagnostics(true))
            .AddSyntaxTrees(probeTree);
        var semanticModel =
            probeCompilation.GetSemanticModel(probeTree);
        var objectCreation = probeTree
            .GetRoot(cancellationToken)
            .DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .First();
        var boundConstructor = semanticModel
            .GetSymbolInfo(
                objectCreation,
                cancellationToken)
            .Symbol as IMethodSymbol;

        return boundConstructor is not null &&
               ConventionConstructorMappingPlanner
                   .AreSameConstructor(
                       boundConstructor,
                       selectedConstructor);
    }

    private static string BuildArgumentExpression(
        ExpressionSyntax expression,
        IParameterSymbol destinationParameter,
        CSharpCompilation compilation,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, string> rewriteExpression,
        CancellationToken cancellationToken)
    {
        if (!TryGetConstructorMemberCast(
                expression,
                destinationParameter,
                compilation,
                semanticModel,
                cancellationToken,
                out var constructorMemberCast))
        {
            return rewriteExpression(expression);
        }

        var parameterTypeName = destinationParameter.Type
            .WithNullableAnnotation(
                destinationParameter.NullableAnnotation)
            .ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable);

        return SyntaxFactory.ParseExpression(
                $"({parameterTypeName})" +
                rewriteExpression(
                    constructorMemberCast.Expression))
            .WithoutTrivia()
            .NormalizeWhitespace()
            .ToFullString();
    }

    private static string BuildProbeArgumentExpression(
        ExpressionSyntax expression,
        CSharpCompilation compilation,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, string> rewriteExpression,
        CancellationToken cancellationToken)
    {
        if (!TryGetConstructorMemberCast(
                expression,
                compilation,
                semanticModel,
                cancellationToken,
                out var constructorMemberCast,
                out var castType))
        {
            return rewriteExpression(expression);
        }

        return SyntaxFactory.ParseExpression(
                "(" +
                castType.ToDisplayString(
                    SymbolDisplayFormats.FullyQualifiedNullable) +
                ")" +
                rewriteExpression(
                    constructorMemberCast.Expression))
            .WithoutTrivia()
            .NormalizeWhitespace()
            .ToFullString();
    }

    private static bool TryGetConstructorMemberCast(
        ExpressionSyntax expression,
        IParameterSymbol destinationParameter,
        CSharpCompilation compilation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out CastExpressionSyntax constructorMemberCast)
    {
        if (!TryGetConstructorMemberCast(
                expression,
                compilation,
                semanticModel,
                cancellationToken,
                out constructorMemberCast,
                out var castType) ||
            castType.TypeArguments.Length != 1 ||
            !TypeMapperMappingTypePolicy.AreEquivalent(
                castType.TypeArguments[0],
                destinationParameter.Type))
        {
            constructorMemberCast = null!;
            return false;
        }

        return true;
    }

    private static bool TryGetConstructorMemberCast(
        ExpressionSyntax expression,
        CSharpCompilation compilation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out CastExpressionSyntax constructorMemberCast,
        out INamedTypeSymbol castType)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        if (expression is not CastExpressionSyntax cast)
        {
            constructorMemberCast = null!;
            castType = null!;
            return false;
        }

        constructorMemberCast = cast;

        if (compilation.GetTypeByMetadataName(
                ConstructorMemberMetadataName) is not
                { } constructorMemberDefinition ||
            semanticModel.GetTypeInfo(
                    constructorMemberCast.Type,
                    cancellationToken)
                .Type is not INamedTypeSymbol resolvedCastType ||
            !SymbolEqualityComparer.Default.Equals(
                resolvedCastType.OriginalDefinition,
                constructorMemberDefinition))
        {
            constructorMemberCast = null!;
            castType = null!;
            return false;
        }

        castType = resolvedCastType;
        return true;
    }

    private static string MakeUnique(
        string candidate,
        HashSet<string> usedNames)
    {
        if (usedNames.Add(candidate))
        {
            return candidate;
        }

        for (var suffix = 2;; suffix++)
        {
            var name = candidate + suffix;

            if (usedNames.Add(name))
            {
                return name;
            }
        }
    }

    private static HashSet<string> BuildUsedProbeNames(
        INamedTypeSymbol mapperType)
    {
        var result = new HashSet<string>(
            mapperType.GetMembers().Select(static member => member.Name),
            StringComparer.Ordinal);

        for (var type = mapperType;
             type is not null;
             type = type.ContainingType)
        {
            foreach (var typeParameter in type.TypeParameters)
            {
                result.Add(typeParameter.Name);
            }
        }

        return result;
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
}

internal readonly record struct TemplateConstructorMappingPlan(
    IMethodSymbol Constructor,
    ImmutableArray<TypeMapperConstructorArgumentMappingModel> Arguments);
