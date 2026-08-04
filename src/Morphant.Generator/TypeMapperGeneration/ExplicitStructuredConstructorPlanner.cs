using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ExplicitStructuredConstructorPlanner
{
    private const string ConstructorParameterMetadataName =
        "Morphant.Members.ConstructorParameter`1";

    public static ExplicitStructuredConstructorPlan? Build(
        ImmutableArray<StructuredObjectArgument> planArguments,
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, string?> rewriteExpression,
        CancellationToken cancellationToken)
    {
        if (destination.TypeKind == TypeKind.Interface ||
            destination.IsAbstract ||
            planArguments.Any(argument =>
                !argument.Syntax.RefKindKeyword.IsKind(
                    SyntaxKind.None)))
        {
            return null;
        }

        var constructors =
            DestinationCapabilityPolicy.GetSupportedConstructors(
                destination,
                compilation,
                cancellationToken);

        if (constructors.IsEmpty)
        {
            return null;
        }

        var usedNames = BuildUsedProbeNames(mapperType);
        var probeTypeName = MakeUnique(
            "__MorphantConstructProbe",
            usedNames);
        var probeMethodName = MakeUnique(
            "__MorphantBindConstruct",
            usedNames);
        var destinationProbeMethodName = MakeUnique(
            "__MorphantBindDestinationConstructor",
            usedNames);
        var probeTree = BuildProbeTree(
            sourceType,
            destination,
            constructors,
            planArguments,
            mapperType,
            probeTypeName,
            probeMethodName,
            expression => BuildProbeArgumentExpression(
                expression,
                compilation,
                semanticModel,
                rewriteExpression,
                cancellationToken));

        if (probeTree is null)
        {
            return null;
        }

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
        var probeArgumentList = probeObjectCreation.ArgumentList;

        if (probeArgumentList is null ||
            probeArgumentList.Arguments.Count != planArguments.Length)
        {
            return null;
        }

        var arguments =
            ImmutableArray.CreateBuilder<
                TypeMapperConstructorArgumentMappingModel>(
                planArguments.Length);
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

            var probeArgument = probeArgumentList.Arguments[index];

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
            var planArgument = planArguments[index];

            if (DeclarativeConstructorMarker.TryGetKind(
                    planArgument.Value,
                    semanticModel,
                    cancellationToken,
                    out var markerKind))
            {
                if (markerKind ==
                    DeclarativeConstructorMarkerKind.Map)
                {
                    return null;
                }

                if (markerKind ==
                    DeclarativeConstructorMarkerKind.Ignore)
                {
                    if (!ConventionConstructorMappingPlanner.CanOmit(
                            destinationParameter))
                    {
                        return null;
                    }

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

            var explicitValueExpression = RewriteArgumentExpression(
                planArgument.Value,
                destinationParameter,
                compilation,
                semanticModel,
                rewriteExpression,
                cancellationToken);

            if (explicitValueExpression is null)
            {
                return null;
            }

            arguments.Add(
                new TypeMapperConstructorArgumentMappingModel(
                    destinationParameter.Name,
                    SourceMemberName: string.Empty,
                    ValueLocalName: null,
                    explicitValueExpression,
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
                compilation,
                mapperType,
                destinationProbeMethodName,
                cancellationToken))
        {
            return null;
        }

        return new ExplicitStructuredConstructorPlan(
            destinationConstructor,
            argumentModels);
    }

    private static SyntaxTree? BuildProbeTree(
        ITypeSymbol sourceType,
        INamedTypeSymbol destination,
        ImmutableArray<IMethodSymbol> constructors,
        ImmutableArray<StructuredObjectArgument> arguments,
        INamedTypeSymbol mapperType,
        string probeTypeName,
        string probeMethodName,
        Func<ExpressionSyntax, string?> rewriteExpression)
    {
        var rewrittenArguments = new string[arguments.Length];

        for (var index = 0; index < arguments.Length; index++)
        {
            if (rewriteExpression(arguments[index].Value) is not
                { } rewritten)
            {
                return null;
            }

            rewrittenArguments[index] = rewritten;
        }

        var sourceTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                sourceType);
        var destinationTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                destination);

        return MapperProbeSyntax.Build(
            mapperType,
            "Morphant.StructuredConstructProbe.g.cs",
            writer =>
            {
                writer.OpenBlock(
                    $"private sealed class {probeTypeName}");

                foreach (var constructor in constructors)
                {
                    var parameters = constructor.Parameters
                        .Select(BuildProbeParameter)
                        .ToArray();

                    writer.Line(
                        $"public {probeTypeName}(" +
                        string.Join(", ", parameters) +
                        ") { }");
                }

                writer.CloseBlock();
                writer.Line();
                writer.Line(
                    $"private {probeTypeName} {probeMethodName}(" +
                    $"{sourceTypeName} source, " +
                    "global::Morphant.Option<" +
                    destinationTypeName +
                    "> previous)");
                writer.Line("{");
                writer.Indent();

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
                            rewrittenArguments[index] +
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
            .WithNullableAnnotation(parameter.NullableAnnotation)
            .ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable);
        var optionalSuffix =
            parameter.IsOptional || parameter.IsParams
                ? " = null!"
                : string.Empty;

        return
            "global::Morphant.Members.ConstructorParameter<" +
            parameterType +
            "> " +
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
            "Morphant.StructuredDestinationConstructorProbe.g.cs",
            writer =>
            {
                writer.Line(
                    $"private {destinationTypeName} {probeMethodName}(" +
                    $"{sourceTypeName} source, " +
                    "global::Morphant.Option<" +
                    destinationTypeName +
                    "> previous)");
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
                             "source." +
                             Identifier(argument.SourceMemberName)) +
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
            !ConventionConstructorMappingPlanner.AreSameConstructor(
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

    internal static string? RewriteArgumentExpression(
        ExpressionSyntax expression,
        IParameterSymbol destinationParameter,
        CSharpCompilation compilation,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, string?> rewriteExpression,
        CancellationToken cancellationToken)
    {
        if (!TryGetConstructorParameterCast(
                expression,
                destinationParameter,
                compilation,
                semanticModel,
                cancellationToken,
                out var constructorParameterCast))
        {
            return rewriteExpression(expression);
        }

        if (rewriteExpression(constructorParameterCast.Expression) is not
            { } rewrittenOperand)
        {
            return null;
        }

        var parameterTypeName = destinationParameter.Type
            .WithNullableAnnotation(
                destinationParameter.NullableAnnotation)
            .ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable);

        return SyntaxFactory.ParseExpression(
                $"({parameterTypeName})" +
                rewrittenOperand)
            .WithoutTrivia()
            .NormalizeWhitespace()
            .ToFullString();
    }

    private static string? BuildProbeArgumentExpression(
        ExpressionSyntax expression,
        CSharpCompilation compilation,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, string?> rewriteExpression,
        CancellationToken cancellationToken)
    {
        if (!TryGetConstructorParameterCast(
                expression,
                compilation,
                semanticModel,
                cancellationToken,
                out var constructorParameterCast,
                out var castType))
        {
            return rewriteExpression(expression);
        }

        if (rewriteExpression(constructorParameterCast.Expression) is not
            { } rewrittenOperand)
        {
            return null;
        }

        return SyntaxFactory.ParseExpression(
                "(" +
                castType.ToDisplayString(
                    SymbolDisplayFormats.FullyQualifiedNullable) +
                ")" +
                rewrittenOperand)
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
        out CastExpressionSyntax constructorParameterCast)
    {
        if (!TryGetConstructorParameterCast(
                expression,
                compilation,
                semanticModel,
                cancellationToken,
                out constructorParameterCast,
                out var castType) ||
            castType.TypeArguments.Length != 1 ||
            !MappingTypeIdentityPolicy.AreEquivalent(
                castType.TypeArguments[0],
                destinationParameter.Type))
        {
            constructorParameterCast = null!;
            return false;
        }

        return true;
    }

    private static bool TryGetConstructorParameterCast(
        ExpressionSyntax expression,
        CSharpCompilation compilation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out CastExpressionSyntax constructorParameterCast,
        out INamedTypeSymbol castType)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        if (expression is not CastExpressionSyntax cast)
        {
            constructorParameterCast = null!;
            castType = null!;
            return false;
        }

        if (compilation.GetTypeByMetadataName(
                ConstructorParameterMetadataName) is not
            { } constructorParameterDefinition ||
            semanticModel.GetTypeInfo(
                    cast.Type,
                    cancellationToken)
                .Type is not INamedTypeSymbol resolvedCastType ||
            !SymbolEqualityComparer.Default.Equals(
                resolvedCastType.OriginalDefinition,
                constructorParameterDefinition))
        {
            constructorParameterCast = null!;
            castType = null!;
            return false;
        }

        constructorParameterCast = cast;
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

        for (var suffix = 1;; suffix++)
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
            mapperType.GetMembers().Select(member => member.Name),
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
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) !=
                   SyntaxKind.None
            ? "@" + value
            : value;
    }
}

internal readonly record struct ExplicitStructuredConstructorPlan(
    IMethodSymbol Constructor,
    ImmutableArray<TypeMapperConstructorArgumentMappingModel> Arguments);
