using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TemplateConstructorMappingPlanner
{
    private const string ConstructorParameterMetadataName =
        "Morphant.Members.ConstructorParameter`1";

    public static TemplateConstructorMappingPlan? Build(
        ImmutableArray<TemplateObjectArgumentSyntax> templateArguments,
        ImmutableArray<TemplateRuntimeLocalPlan> runtimeLocals,
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        SemanticModel templateSemanticModel,
        Func<ExpressionSyntax, string> rewriteExpression,
        Func<ExpressionSyntax, bool>
            isKnownAbsentExistingDestination,
        CancellationToken cancellationToken)
    {
        if (destination.TypeKind == TypeKind.Interface ||
            destination.IsAbstract ||
            templateArguments.Any(
                static argument =>
                    !argument.Syntax.RefKindKeyword.IsKind(
                        SyntaxKind.None)))
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
            templateArguments,
            runtimeLocals,
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
        if (probeMethod.Body?.Statements.LastOrDefault() is not
            ReturnStatementSyntax
            {
                Expression:
                    ObjectCreationExpressionSyntax probeObjectCreation
            })
        {
            return null;
        }
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
                templateArguments.Length)
        {
            return null;
        }

        var arguments =
            ImmutableArray.CreateBuilder<
                TypeMapperConstructorArgumentMappingModel>(
                templateArguments.Length);
        var ignoredParameterNames =
            ImmutableArray.CreateBuilder<string>();
        var sourceMembers =
            ConventionMemberMappingPlanner.BuildReadableMembers(
                sourceType,
                compilation,
                mapperType,
                cancellationToken);

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
                templateArguments[index];

            if (TemplateMemberMarker.TryGetKind(
                    templateArgument.Value,
                    templateSemanticModel,
                    cancellationToken,
                    out var markerKind))
            {
                if (markerKind == TemplateMemberMarkerKind.Ignore)
                {
                    if (!ConventionConstructorMappingPlanner.CanOmit(
                            destinationParameter))
                    {
                        return null;
                    }

                    ignoredParameterNames.Add(
                        destinationParameter.Name);
                    continue;
                }

                if (ConventionConstructorMappingPlanner
                        .TryFindSourceMember(
                            sourceMembers,
                            destinationParameter.Name) is not
                    { } sourceMember ||
                    !MappingExpressionCompatibility
                        .HasPotentiallyCompatibleConversion(
                            sourceMember.Type,
                            destinationParameter.Type,
                            compilation))
                {
                    return null;
                }

                arguments.Add(
                    new TypeMapperConstructorArgumentMappingModel(
                        destinationParameter.Name,
                        sourceMember.Name,
                        ValueLocalName: null,
                        TargetTypeName:
                            ConventionConstructorMappingPlanner
                                .BuildTargetValueLocalTypeName(
                                    destinationParameter)));
                continue;
            }

            if (TemplateNestedMapMappingPlanner.TryRecognize(
                    templateArgument.Value,
                    sourceType,
                    compilation,
                    mapperType,
                    templateSemanticModel,
                    rewriteExpression,
                    isKnownAbsentExistingDestination,
                    cancellationToken,
                    out var nestedMap))
            {
                if (nestedMap is not { } nestedMapValue ||
                    !TemplateNestedMapMappingPlanner
                        .TryBuildValueExpression(
                            nestedMapValue,
                            destinationParameter.Type
                                .WithNullableAnnotation(
                                    destinationParameter
                                        .NullableAnnotation),
                            out var nestedMapExpression))
                {
                    return null;
                }

                arguments.Add(
                    new TypeMapperConstructorArgumentMappingModel(
                        destinationParameter.Name,
                        SourceMemberName: string.Empty,
                        ValueLocalName: null,
                        nestedMapExpression,
                        ValueLocalTypeName:
                            ConventionConstructorMappingPlanner
                                .BuildExplicitValueLocalTypeName(
                                    destinationParameter),
                        TargetTypeName:
                            ConventionConstructorMappingPlanner
                                .BuildTargetValueLocalTypeName(
                                    destinationParameter)));
                continue;
            }

            arguments.Add(
                new TypeMapperConstructorArgumentMappingModel(
                    destinationParameter.Name,
                    SourceMemberName: string.Empty,
                    ValueLocalName: null,
                    BuildArgumentExpression(
                        templateArgument.Value,
                        destinationParameter,
                        compilation,
                        templateSemanticModel,
                        rewriteExpression,
                        cancellationToken),
                    ValueLocalTypeName:
                        ConventionConstructorMappingPlanner
                            .BuildExplicitValueLocalTypeName(
                                destinationParameter),
                    TargetTypeName:
                        ConventionConstructorMappingPlanner
                            .BuildTargetValueLocalTypeName(
                                destinationParameter)));
        }

        var argumentModels = arguments.ToImmutable();

        if (!BindsDestinationConstructor(
                sourceType,
                destination,
                destinationConstructor,
                argumentModels,
                runtimeLocals,
                compilation,
                mapperType,
                destinationProbeMethodName,
                cancellationToken))
        {
            return null;
        }

        return new TemplateConstructorMappingPlan(
            destinationConstructor,
            argumentModels,
            ignoredParameterNames.ToImmutable());
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
        ImmutableArray<TemplateObjectArgumentSyntax> arguments,
        ImmutableArray<TemplateRuntimeLocalPlan> runtimeLocals,
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
                    $"{probeMethodName}(" +
                    $"{sourceTypeName} source, " +
                    "global::Morphant.Context.MappingContext context)");
                writer.Line("{");
                writer.Indent();

                WriteRuntimeLocals(
                    writer,
                    runtimeLocals);

                if (arguments.IsEmpty)
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
                         index < arguments.Length;
                         index++)
                    {
                        var argument = arguments[index];
                        var prefix =
                            argument.Syntax.NameColon is { } nameColon
                                ? nameColon.Name.Identifier.Text + ": "
                                : string.Empty;
                        var suffix = index < arguments.Length - 1
                            ? ","
                            : ");";

                        writer.Line(
                            prefix +
                            rewriteExpression(argument.Value) +
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
            $"global::Morphant.Members.ConstructorParameter<{parameterType}> " +
            Identifier(parameter.Name) +
            optionalSuffix;
    }

    private static bool BindsDestinationConstructor(
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        IMethodSymbol selectedConstructor,
        ImmutableArray<TypeMapperConstructorArgumentMappingModel> arguments,
        ImmutableArray<TemplateRuntimeLocalPlan> runtimeLocals,
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
                    $"{probeMethodName}(" +
                    $"{sourceTypeName} source, " +
                    "global::Morphant.Context.MappingContext context)");
                writer.Line("{");
                writer.Indent();

                WriteRuntimeLocals(
                    writer,
                    runtimeLocals);

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
                             $"source!.{Identifier(argument.SourceMemberName)}") +
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
        var probeMethod = probeTree
            .GetRoot(cancellationToken)
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(method =>
                method.Identifier.ValueText == probeMethodName);

        if (probeMethod.Body?.Statements.LastOrDefault() is not
            ReturnStatementSyntax
            {
                Expression:
                    ObjectCreationExpressionSyntax objectCreation
            })
        {
            return false;
        }
        var boundConstructor = semanticModel
            .GetSymbolInfo(
                objectCreation,
                cancellationToken)
            .Symbol as IMethodSymbol;

        if (boundConstructor is null ||
            !ConventionConstructorMappingPlanner
                .AreSameConstructor(
                    boundConstructor,
                    selectedConstructor))
        {
            return false;
        }

        var diagnostics = semanticModel.GetDiagnostics(
            cancellationToken: cancellationToken);

        for (var index = 0;
             index < arguments.Length;
             index++)
        {
            if (arguments[index].ExplicitValueExpression is null &&
                MappingExpressionCompatibility.HasNullableWarning(
                    diagnostics,
                    objectCreation.ArgumentList!.Arguments[index].Span))
            {
                return false;
            }
        }

        return true;
    }

    private static void WriteRuntimeLocals(
        CodeWriter writer,
        ImmutableArray<TemplateRuntimeLocalPlan> runtimeLocals)
    {
        foreach (var local in runtimeLocals)
        {
            writer.Line(
                $"{local.DeclarationType} " +
                $"{local.PlaceholderName} = " +
                $"{local.MapNewExpression};");
        }

        if (!runtimeLocals.IsEmpty)
        {
            writer.Line();
        }
    }

    private static string BuildArgumentExpression(
        ExpressionSyntax expression,
        IParameterSymbol destinationParameter,
        CSharpCompilation compilation,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, string> rewriteExpression,
        CancellationToken cancellationToken)
    {
        if (!TryGetConstructorParameterCast(
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
        if (!TryGetConstructorParameterCast(
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

    private static bool TryGetConstructorParameterCast(
        ExpressionSyntax expression,
        IParameterSymbol destinationParameter,
        CSharpCompilation compilation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out CastExpressionSyntax constructorMemberCast)
    {
        if (!TryGetConstructorParameterCast(
                expression,
                compilation,
                semanticModel,
                cancellationToken,
                out constructorMemberCast,
                out var castType) ||
            castType.TypeArguments.Length != 1 ||
            !MappingTypeIdentityPolicy.AreEquivalent(
                castType.TypeArguments[0],
                destinationParameter.Type))
        {
            constructorMemberCast = null!;
            return false;
        }

        return true;
    }

    private static bool TryGetConstructorParameterCast(
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
                ConstructorParameterMetadataName) is not
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
    ImmutableArray<TypeMapperConstructorArgumentMappingModel> Arguments,
    ImmutableArray<string> IgnoredParameterNames);
