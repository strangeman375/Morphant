using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TemplateByConventionMappingPlanner
{
    private const string ByConventionMarkerMetadataName =
        "Morphant.Markers.ByConventionMarker";

    public static bool TryBuild(
        ImplicitObjectCreationExpressionSyntax objectCreation,
        ITypeSymbol sourceType,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, string> rewriteExpression,
        Func<ExpressionSyntax, bool>
            isKnownAbsentExistingDestination,
        CancellationToken cancellationToken,
        out ImmutableArray<TemplateConstructorMemberMappingModel>
            mappings)
    {
        mappings = default;

        if (!ContainsMarker(
                objectCreation,
                semanticModel,
                cancellationToken))
        {
            return false;
        }

        if (objectCreation.ArgumentList.Arguments.Count is < 1 or > 2)
        {
            return true;
        }

        ArgumentSyntax? markerArgument = null;
        ArgumentSyntax? membersArgument = null;

        foreach (var argument in
                 objectCreation.ArgumentList.Arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsMarker(
                    argument.Expression,
                    semanticModel,
                    cancellationToken))
            {
                if (markerArgument is not null ||
                    argument.NameColon is
                        { Name.Identifier.ValueText: not "marker" })
                {
                    return true;
                }

                markerArgument = argument;
                continue;
            }

            if (membersArgument is not null ||
                argument.NameColon is
                    { Name.Identifier.ValueText: not "members" })
            {
                return true;
            }

            membersArgument = argument;
        }

        if (markerArgument is null)
        {
            return true;
        }

        if (membersArgument is null ||
            IsOmittedMembersValue(membersArgument.Expression))
        {
            mappings = [];
            return true;
        }

        if (UnwrapParentheses(membersArgument.Expression) is not
                BaseObjectCreationExpressionSyntax membersCreation ||
            membersCreation.ArgumentList?.Arguments.Count > 0)
        {
            return true;
        }

        var result =
            ImmutableArray.CreateBuilder<
                TemplateConstructorMemberMappingModel>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var expression in
                 membersCreation.Initializer?.Expressions ?? default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (expression is not AssignmentExpressionSyntax
                {
                    RawKind: (int)SyntaxKind.SimpleAssignmentExpression,
                    Left: IdentifierNameSyntax memberName,
                    Right: var value
                } ||
                !seenNames.Add(memberName.Identifier.ValueText))
            {
                return true;
            }

            if (TemplateMemberMarker.TryGetKind(
                    value,
                    semanticModel,
                    cancellationToken,
                    out var markerKind))
            {
                result.Add(
                    new TemplateConstructorMemberMappingModel(
                        memberName.Identifier.ValueText,
                        markerKind,
                        ExplicitValueExpression: null));
            }
            else if (TemplateNestedMapMappingPlanner.TryRecognize(
                         value,
                         sourceType,
                         compilation,
                         mapperType,
                         semanticModel,
                         rewriteExpression,
                         isKnownAbsentExistingDestination,
                         cancellationToken,
                         out var nestedMap))
            {
                if (nestedMap is not { } nestedMapValue)
                {
                    return true;
                }

                result.Add(
                    new TemplateConstructorMemberMappingModel(
                        memberName.Identifier.ValueText,
                        MarkerKind: null,
                        ExplicitValueExpression: null,
                        nestedMapValue));
            }
            else
            {
                result.Add(
                    new TemplateConstructorMemberMappingModel(
                        memberName.Identifier.ValueText,
                        MarkerKind: null,
                        rewriteExpression(value)));
            }
        }

        mappings = result.ToImmutable();
        return true;
    }

    private static bool ContainsMarker(
        ImplicitObjectCreationExpressionSyntax objectCreation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var argument in
                 objectCreation.ArgumentList.Arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsMarker(
                    argument.Expression,
                    semanticModel,
                    cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMarker(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        expression = UnwrapParentheses(expression);

        if (semanticModel.GetTypeInfo(
                    expression,
                    cancellationToken)
                .Type is INamedTypeSymbol type &&
            IsMarkerType(type))
        {
            return true;
        }

        return semanticModel.GetSymbolInfo(
                    expression,
                    cancellationToken)
                .Symbol is IMethodSymbol
                {
                    ReturnType: INamedTypeSymbol returnType
                } &&
               IsMarkerType(returnType);
    }

    private static bool IsMarkerType(
        INamedTypeSymbol type)
    {
        return StringComparer.Ordinal.Equals(
            SymbolNameHelper.GetFullMetadataName(type),
            ByConventionMarkerMetadataName);
    }

    private static bool IsOmittedMembersValue(
        ExpressionSyntax expression)
    {
        return UnwrapParentheses(expression) is
            LiteralExpressionSyntax
            {
                RawKind:
                    (int)SyntaxKind.NullLiteralExpression or
                    (int)SyntaxKind.DefaultLiteralExpression
            } or
            DefaultExpressionSyntax;
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

internal readonly record struct TemplateConstructorMemberMappingModel(
    string ParameterName,
    TemplateMemberMarkerKind? MarkerKind,
    string? ExplicitValueExpression,
    TemplateNestedMapMapping? NestedMap = null);
