using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TemplateNestedMapMappingPlanner
{
    private const string TypeMapperMetadataName =
        "Morphant.TypeMapper";

    public static bool TryRecognize(
        ExpressionSyntax expression,
        ITypeSymbol containingSourceType,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, string> rewriteExpression,
        Func<ExpressionSyntax, bool>
            isKnownAbsentExistingDestination,
        CancellationToken cancellationToken,
        out TemplateNestedMapMapping? mapping)
    {
        mapping = null;
        expression = UnwrapParentheses(expression);

        if (expression is not InvocationExpressionSyntax invocation ||
            !TryGetMapMethod(
                invocation,
                semanticModel,
                cancellationToken,
                out var method))
        {
            return false;
        }

        if (method.Arity > 1)
        {
            return false;
        }

        if (method.Parameters.Length is < 1 or > 2 ||
            invocation.ArgumentList.Arguments.Count !=
            method.Parameters.Length)
        {
            return true;
        }

        ExpressionSyntax? sourceExpression = null;
        ExpressionSyntax? destinationExpression = null;
        var sourceArgumentIndex = -1;
        var destinationArgumentIndex = -1;

        for (var index = 0;
             index < invocation.ArgumentList.Arguments.Count;
             index++)
        {
            var argument =
                invocation.ArgumentList.Arguments[index];
            var parameterOrdinal =
                TryGetParameterOrdinal(
                    argument,
                    method,
                    index);

            if (parameterOrdinal is null)
            {
                return true;
            }

            switch (parameterOrdinal.Value)
            {
                case 0 when sourceExpression is null:
                    sourceExpression = argument.Expression;
                    sourceArgumentIndex = index;
                    break;

                case 1 when destinationExpression is null:
                    destinationExpression = argument.Expression;
                    destinationArgumentIndex = index;
                    break;

                default:
                    return true;
            }
        }

        if (sourceExpression is null)
        {
            return true;
        }

        var sourceType = TryGetStaticType(
                sourceExpression,
                semanticModel,
                cancellationToken);
        string? rewrittenSourceExpression = null;

        if (sourceType is null)
        {
            rewrittenSourceExpression =
                rewriteExpression(sourceExpression);
            sourceType = TryGetStaticTypeFromProbe(
                rewrittenSourceExpression,
                containingSourceType,
                compilation,
                mapperType,
                cancellationToken);
        }

        if (sourceType is null)
        {
            return true;
        }

        if (CanApplyNullableAnnotation(sourceType) &&
            HasExplicitNullableType(sourceExpression))
        {
            sourceType = sourceType.WithNullableAnnotation(
                NullableAnnotation.Annotated);
        }

        rewrittenSourceExpression ??=
            RewriteMapArgument(
                sourceExpression,
                sourceType,
                semanticModel,
                rewriteExpression,
                cancellationToken);

        ITypeSymbol? explicitDestinationType = null;

        if (method.Arity == 1)
        {
            if (TryGetExplicitDestinationType(
                    invocation,
                    method,
                    semanticModel,
                    cancellationToken) is not { } type)
            {
                return true;
            }

            explicitDestinationType = type;
        }

        var destinationIsKnownAbsent =
            destinationExpression is not null &&
            isKnownAbsentExistingDestination(
                destinationExpression);

        mapping = new TemplateNestedMapMapping(
            sourceType,
            rewrittenSourceExpression,
            explicitDestinationType,
            destinationExpression is null ||
            destinationIsKnownAbsent
                ? null
                : RewriteExistingDestinationArgument(
                    destinationExpression,
                    semanticModel,
                    rewriteExpression,
                    cancellationToken),
            destinationArgumentIndex >= 0 &&
            destinationArgumentIndex < sourceArgumentIndex);

        return true;
    }

    private static string RewriteExistingDestinationArgument(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, string> rewriteExpression,
        CancellationToken cancellationToken)
    {
        var staticType = TryGetStaticType(
            expression,
            semanticModel,
            cancellationToken);

        if (staticType is not null)
        {
            if (CanApplyNullableAnnotation(staticType) &&
                HasExplicitNullableType(expression))
            {
                staticType = staticType.WithNullableAnnotation(
                    NullableAnnotation.Annotated);
            }

            return RewriteMapArgument(
                expression,
                staticType,
                semanticModel,
                rewriteExpression,
                cancellationToken);
        }

        return IsNullLike(expression)
            ? expression.WithoutTrivia().ToFullString()
            : rewriteExpression(expression);
    }

    private static bool HasExplicitNullableType(
        ExpressionSyntax expression)
    {
        expression = UnwrapParentheses(expression);

        return expression is CastExpressionSyntax
            {
                Type: NullableTypeSyntax
            } or
            DefaultExpressionSyntax
            {
                Type: NullableTypeSyntax
            };
    }

    private static string RewriteMapArgument(
        ExpressionSyntax expression,
        ITypeSymbol staticType,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, string> rewriteExpression,
        CancellationToken cancellationToken)
    {
        var constant = semanticModel.GetConstantValue(
            expression,
            cancellationToken);

        return constant is
        {
            HasValue: true,
            Value: null
        }
            ? "(" +
              TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                  staticType) +
              ")null"
            : rewriteExpression(expression);
    }

    private static bool TryGetMapMethod(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out IMethodSymbol method)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(
            invocation,
            cancellationToken);
        var candidates =
            new HashSet<IMethodSymbol>(
                SymbolEqualityComparer.Default);

        AddCandidates(symbolInfo, candidates);

        var invokedName = invocation.Expression switch
        {
            SimpleNameSyntax simpleName => simpleName,
            MemberAccessExpressionSyntax
            {
                Name: var simpleName
            } => simpleName,
            _ => null
        };

        if (invokedName is not null)
        {
            AddCandidates(
                semanticModel.GetSymbolInfo(
                    invokedName,
                    cancellationToken),
                candidates);
        }

        var arity = invocation.Expression switch
        {
            GenericNameSyntax genericName =>
                genericName.TypeArgumentList.Arguments.Count,
            MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax genericName
            } =>
                genericName.TypeArgumentList.Arguments.Count,
            _ => 0
        };
        IMethodSymbol? result = null;

        foreach (var candidate in candidates)
        {
            if (candidate.Name != "Map" ||
                candidate.Arity != arity ||
                candidate.Parameters.Length !=
                invocation.ArgumentList.Arguments.Count ||
                !StringComparer.Ordinal.Equals(
                    SymbolNameHelper.GetFullMetadataName(
                        candidate.ContainingType),
                    TypeMapperMetadataName))
            {
                continue;
            }

            if (result is not null)
            {
                method = null!;
                return false;
            }

            result = candidate;
        }

        method = result!;
        return result is not null;
    }

    private static void AddCandidates(
        SymbolInfo symbolInfo,
        HashSet<IMethodSymbol> candidates)
    {
        if (symbolInfo.Symbol is IMethodSymbol method)
        {
            candidates.Add(method);
        }

        foreach (var candidate in
                 symbolInfo.CandidateSymbols
                     .OfType<IMethodSymbol>())
        {
            candidates.Add(candidate);
        }
    }

    private static int? TryGetParameterOrdinal(
        ArgumentSyntax argument,
        IMethodSymbol method,
        int positionalOrdinal)
    {
        if (argument.NameColon is not { } nameColon)
        {
            return positionalOrdinal < method.Parameters.Length
                ? positionalOrdinal
                : null;
        }

        var parameterName =
            nameColon.Name.Identifier.ValueText;

        for (var index = 0;
             index < method.Parameters.Length;
             index++)
        {
            if (StringComparer.Ordinal.Equals(
                    method.Parameters[index].Name,
                    parameterName))
            {
                return index;
            }
        }

        return null;
    }

    private static ITypeSymbol? TryGetExplicitDestinationType(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        TypeSyntax? typeSyntax = invocation.Expression switch
        {
            GenericNameSyntax
            {
                TypeArgumentList.Arguments.Count: 1
            } genericName =>
                genericName.TypeArgumentList.Arguments[0],
            MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax
                {
                    TypeArgumentList.Arguments.Count: 1
                } genericName
            } =>
                genericName.TypeArgumentList.Arguments[0],
            _ => null
        };

        if (typeSyntax is not null)
        {
            var syntaxType = semanticModel.GetTypeInfo(
                    typeSyntax,
                    cancellationToken)
                .Type;

            if (syntaxType is
                {
                    TypeKind: not TypeKind.Error
                })
            {
                return ApplyExplicitTypeNullability(
                    syntaxType,
                    typeSyntax);
            }

            if (semanticModel.GetSymbolInfo(
                    typeSyntax,
                    cancellationToken)
                .Symbol is ITypeSymbol symbolType)
            {
                return ApplyExplicitTypeNullability(
                    symbolType,
                    typeSyntax);
            }
        }

        return method.TypeArguments.Length == 1 &&
               method.TypeArguments[0] is not
                   ITypeParameterSymbol
            ? method.TypeArguments[0]
            : null;
    }

    private static ITypeSymbol ApplyExplicitTypeNullability(
        ITypeSymbol type,
        TypeSyntax syntax)
    {
        return CanApplyNullableAnnotation(type) &&
               syntax is NullableTypeSyntax
            ? type.WithNullableAnnotation(
                NullableAnnotation.Annotated)
            : type;
    }

    private static ITypeSymbol? TryGetStaticTypeFromProbe(
        string expression,
        ITypeSymbol sourceType,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var usedNames = new HashSet<string>(
            mapperType.GetMembers()
                .Select(static member => member.Name),
            StringComparer.Ordinal);

        for (var current = mapperType;
             current is not null;
             current = current.ContainingType)
        {
            foreach (var typeParameter in
                     current.TypeParameters)
            {
                usedNames.Add(typeParameter.Name);
            }
        }

        var methodName = MakeUnique(
            "__MorphantNestedMapTypeProbe",
            usedNames);
        var valueName = MakeUnique(
            "__morphantNestedMapValue",
            usedNames);
        var sourceTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                sourceType);
        var probeTree = MapperProbeSyntax.Build(
            mapperType,
            "Morphant.NestedMapTypeProbe.g.cs",
            writer =>
            {
                writer.Line(
                    $"private void {methodName}(" +
                    $"{sourceTypeName} source)");
                writer.Line("{");
                writer.Indent();
                writer.Line($"var {valueName} = {expression};");
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
        var method = probeTree
            .GetRoot(cancellationToken)
            .DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(candidate =>
                candidate.Identifier.ValueText ==
                methodName);

        if (method is null)
        {
            return null;
        }

        var initializers = method
            .DescendantNodes()
            .OfType<EqualsValueClauseSyntax>()
            .ToArray();

        if (initializers.Length != 1)
        {
            return null;
        }

        var initializer = initializers[0].Value;
        return TryGetStaticType(
            initializer,
            semanticModel,
            cancellationToken);
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
            var name =
                candidate +
                suffix.ToString(CultureInfo.InvariantCulture);

            if (usedNames.Add(name))
            {
                return name;
            }
        }
    }

    public static bool TryBuildValueExpression(
        TemplateNestedMapMapping mapping,
        ITypeSymbol targetDestinationType,
        out string expression)
    {
        expression = string.Empty;

        var destinationType =
            mapping.ExplicitDestinationType ??
            targetDestinationType;

        if (mapping.ExplicitDestinationType is not null &&
            !TypeMapperMappingTypePolicy.AreEquivalent(
                destinationType,
                targetDestinationType))
        {
            return false;
        }

        var sourceTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                mapping.SourceType);
        var destinationTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                destinationType);

        if (mapping.ExistingDestinationExpression is null)
        {
            expression =
                $"context.Mapper.Map<{sourceTypeName}, " +
                $"{destinationTypeName}>(" +
                $"{mapping.SourceExpression}, context)";
            return true;
        }

        expression = mapping.DestinationArgumentBeforeSource
            ? $"context.Mapper.Map<{sourceTypeName}, " +
              $"{destinationTypeName}>(" +
              "destination: " +
              mapping.ExistingDestinationExpression +
              ", source: " +
              mapping.SourceExpression +
              ", context: context)"
            : $"context.Mapper.Map<{sourceTypeName}, " +
              $"{destinationTypeName}>(" +
              mapping.SourceExpression +
              ", " +
              mapping.ExistingDestinationExpression +
              ", context)";

        return true;
    }

    private static ITypeSymbol? TryGetStaticType(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var typeInfo = semanticModel.GetTypeInfo(
            expression,
            cancellationToken);
        var symbolType = TryGetSymbolType(
            semanticModel.GetSymbolInfo(
                expression,
                cancellationToken));

        if (symbolType is not null)
        {
            return ApplyFlowNullability(
                symbolType,
                typeInfo.Nullability);
        }

        var type = typeInfo.Type;

        if (type is
        {
            TypeKind: not TypeKind.Error,
            SpecialType: not SpecialType.System_Void
        })
        {
            return ApplyFlowNullability(
                type.WithNullableAnnotation(
                    typeInfo.Nullability.Annotation),
                typeInfo.Nullability);
        }

        return null;
    }

    private static ITypeSymbol? TryGetSymbolType(
        SymbolInfo symbolInfo)
    {
        IEnumerable<ISymbol> symbols = symbolInfo.Symbol is { } symbol
            ? new[] { symbol }
            : symbolInfo.CandidateSymbols;
        ITypeSymbol? result = null;

        foreach (var candidate in symbols)
        {
            var candidateType = candidate switch
            {
                IFieldSymbol field =>
                    field.Type.WithNullableAnnotation(
                        field.NullableAnnotation),
                ILocalSymbol local =>
                    local.Type.WithNullableAnnotation(
                        local.NullableAnnotation),
                IParameterSymbol parameter =>
                    parameter.Type.WithNullableAnnotation(
                        parameter.NullableAnnotation),
                IPropertySymbol property =>
                    property.Type.WithNullableAnnotation(
                        property.NullableAnnotation),
                IMethodSymbol method =>
                    method.ReturnType.WithNullableAnnotation(
                        method.ReturnNullableAnnotation),
                _ => null
            };

            if (candidateType is null)
            {
                continue;
            }

            if (result is not null &&
                !StringComparer.Ordinal.Equals(
                    TypeMapperMappingTypePolicy
                        .GetGeneratedTypeName(result),
                    TypeMapperMappingTypePolicy
                        .GetGeneratedTypeName(candidateType)))
            {
                return null;
            }

            result = candidateType;
        }

        return result;
    }

    private static ITypeSymbol ApplyFlowNullability(
        ITypeSymbol type,
        NullabilityInfo nullability)
    {
        return CanApplyNullableAnnotation(type) &&
               nullability.FlowState ==
               NullableFlowState.MaybeNull
            ? type.WithNullableAnnotation(
                NullableAnnotation.Annotated)
            : type;
    }

    private static bool CanApplyNullableAnnotation(
        ITypeSymbol type)
    {
        return type.IsReferenceType ||
               type is ITypeParameterSymbol
               {
                   HasValueTypeConstraint: false,
                   HasUnmanagedTypeConstraint: false
               };
    }

    private static bool IsNullLike(
        ExpressionSyntax expression)
    {
        expression = UnwrapParentheses(expression);

        return expression is LiteralExpressionSyntax
        {
            RawKind:
                (int)SyntaxKind.NullLiteralExpression or
                (int)SyntaxKind.DefaultLiteralExpression
        };
    }

    private static ExpressionSyntax UnwrapParentheses(
        ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }
}

internal readonly record struct TemplateNestedMapMapping(
    ITypeSymbol SourceType,
    string SourceExpression,
    ITypeSymbol? ExplicitDestinationType,
    string? ExistingDestinationExpression,
    bool DestinationArgumentBeforeSource);
