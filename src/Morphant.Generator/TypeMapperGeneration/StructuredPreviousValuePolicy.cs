using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class StructuredPreviousValuePolicy
{
    public static bool TryGetOrigin(
        ExpressionSyntax expression,
        IParameterSymbol previous,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ExpressionSyntax origin,
        out ImmutableArray<DeclarativeTerminalAliasSyntax> aliases)
    {
        var resolvedAliases =
            ImmutableArray.CreateBuilder<DeclarativeTerminalAliasSyntax>();
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var current = Unwrap(expression);

        while (semanticModel.GetSymbolInfo(current, cancellationToken).Symbol
                   is ILocalSymbol local &&
               visited.Add(local) &&
               IsUnchanged(local, current, semanticModel, cancellationToken))
        {
            var declaration = local.DeclaringSyntaxReferences
                .FirstOrDefault()?.GetSyntax(cancellationToken);

            if (declaration is SingleVariableDesignationSyntax
                {
                    Parent: DeclarationExpressionSyntax
                    {
                        Parent: ArgumentSyntax
                        {
                            Parent.Parent: InvocationExpressionSyntax invocation
                        }
                    }
                } &&
                IsTryGetValue(invocation, previous, semanticModel,
                    cancellationToken))
            {
                origin = current;
                aliases = resolvedAliases.ToImmutable().Reverse().ToImmutableArray();
                return true;
            }

            if (declaration is not VariableDeclaratorSyntax
                {
                    Initializer.Value: { } initializer
                } ||
                !SymbolEqualityComparer.Default.Equals(
                    local.Type,
                    semanticModel.GetTypeInfo(initializer, cancellationToken).Type))
            {
                break;
            }

            resolvedAliases.Add(new DeclarativeTerminalAliasSyntax(
                current, initializer));
            current = Unwrap(initializer);
        }

        origin = current;
        aliases = resolvedAliases.ToImmutable().Reverse().ToImmutableArray();
        return current is MemberAccessExpressionSyntax access &&
               semanticModel.GetSymbolInfo(access, cancellationToken).Symbol is
                   IPropertySymbol { Name: "Value" } property &&
               SymbolEqualityComparer.Default.Equals(
                   property.ContainingType, previous.Type) &&
               IsOption(access.Expression, previous, semanticModel,
                   cancellationToken);
    }

    public static bool IsOption(
        ExpressionSyntax expression,
        IParameterSymbol previous,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var visited = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        expression = Unwrap(expression);

        while (semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol
                   is ILocalSymbol local &&
               visited.Add(local) &&
               IsUnchanged(local, expression, semanticModel, cancellationToken) &&
               local.DeclaringSyntaxReferences.FirstOrDefault()
                   ?.GetSyntax(cancellationToken) is VariableDeclaratorSyntax
                   {
                       Initializer.Value: { } initializer
                   } &&
               SymbolEqualityComparer.Default.Equals(local.Type, previous.Type))
        {
            expression = Unwrap(initializer);
        }

        return SymbolEqualityComparer.Default.Equals(
            semanticModel.GetSymbolInfo(expression, cancellationToken).Symbol,
            previous);
    }

    public static bool IsTryGetValue(
        InvocationExpressionSyntax invocation,
        IParameterSymbol previous,
        SemanticModel semanticModel,
        CancellationToken cancellationToken) =>
        invocation.Expression is MemberAccessExpressionSyntax access &&
        semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol is
            IMethodSymbol { Name: "TryGetValue", Parameters.Length: 1 } method &&
        method.Parameters[0].RefKind == RefKind.Out &&
        SymbolEqualityComparer.Default.Equals(method.ContainingType, previous.Type) &&
        IsOption(access.Expression, previous, semanticModel, cancellationToken);

    private static bool IsUnchanged(
        ILocalSymbol local,
        ExpressionSyntax use,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var lambda = use.Ancestors().OfType<LambdaExpressionSyntax>().FirstOrDefault();
        if (lambda is null)
        {
            return false;
        }

        foreach (var identifier in lambda.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    local))
            {
                continue;
            }

            foreach (var ancestor in identifier.Ancestors().TakeWhile(node => node != lambda))
            {
                if (ancestor is AssignmentExpressionSyntax assignment &&
                        assignment.Left.Span.Contains(identifier.Span) ||
                    ancestor is ArgumentSyntax argument &&
                        (argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) ||
                         argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword)) ||
                    ancestor is RefExpressionSyntax ||
                    ancestor is PrefixUnaryExpressionSyntax prefix &&
                        (prefix.IsKind(SyntaxKind.PreIncrementExpression) ||
                         prefix.IsKind(SyntaxKind.PreDecrementExpression)) ||
                    ancestor is PostfixUnaryExpressionSyntax postfix &&
                        (postfix.IsKind(SyntaxKind.PostIncrementExpression) ||
                         postfix.IsKind(SyntaxKind.PostDecrementExpression)) ||
                    local.Type.IsValueType &&
                    ancestor is InvocationExpressionSyntax invocation &&
                    invocation.Expression is MemberAccessExpressionSyntax access &&
                    access.Expression.Span.Contains(identifier.Span))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    break;
                case PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    expression = postfix.Operand;
                    break;
                default:
                    return expression;
            }
        }
    }
}
