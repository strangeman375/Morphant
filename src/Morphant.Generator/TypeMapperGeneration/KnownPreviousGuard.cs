using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class KnownPreviousGuard
{
    public static TypeMapperControlFlowNode? TryLower(
        ExpressionSyntax condition,
        TypeMapperControlFlowNode whenTrue,
        TypeMapperControlFlowNode whenFalse,
        IParameterSymbol previous,
        bool available,
        string previousValue,
        SemanticModel semanticModel,
        Func<ExpressionSyntax, TypeMapperRewrittenDependencyExpression?> rewrite,
        Func<ExpressionSyntax, TypeMapperControlFlowNode, TypeMapperControlFlowNode,
            TypeMapperControlFlowNode?> buildCondition,
        CancellationToken cancellationToken)
    {
        if (previous.Type is not INamedTypeSymbol { MetadataName: "Option`1" } option ||
            option.ContainingNamespace.ToDisplayString() != "Morphant") return null;

        bool IsCheck(InvocationExpressionSyntax invocation) =>
            StructuredPreviousValuePolicy.IsTryGetValue(
                invocation, previous, semanticModel, cancellationToken);

        InvocationExpressionSyntax? LeadingCheck(ExpressionSyntax expression)
        {
            expression = Unwrap(expression);
            if (expression is InvocationExpressionSyntax invocation && IsCheck(invocation))
                return invocation;
            if (expression is PrefixUnaryExpressionSyntax prefix &&
                prefix.IsKind(SyntaxKind.LogicalNotExpression) &&
                semanticModel.GetOperation(prefix, cancellationToken) is
                    IUnaryOperation { OperatorMethod: null, Operand.Type.SpecialType: SpecialType.System_Boolean })
                return LeadingCheck(prefix.Operand);
            if (expression is BinaryExpressionSyntax binary && IsBooleanBinary(binary))
                return LeadingCheck(binary.Left);
            return null;
        }

        bool IsBooleanBinary(BinaryExpressionSyntax binary) =>
            (binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression)) &&
            semanticModel.GetOperation(binary, cancellationToken) is IBinaryOperation
            {
                OperatorMethod: null, IsLifted: false,
                LeftOperand.Type.SpecialType: SpecialType.System_Boolean,
                RightOperand.Type.SpecialType: SpecialType.System_Boolean
            } &&
            semanticModel.GetTypeInfo(binary.Left, cancellationToken).Type?.SpecialType == SpecialType.System_Boolean &&
            semanticModel.GetTypeInfo(binary.Right, cancellationToken).Type?.SpecialType == SpecialType.System_Boolean;

        var check = LeadingCheck(condition);
        if (check is null || condition.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                .Count(IsCheck) != 1 || rewrite(check) is not { } rewritten ||
            SyntaxFactory.ParseExpression(rewritten.Expression) is not InvocationExpressionSyntax invocation)
            return null;

        TypeMapperControlFlowNode? Continue(
            ExpressionSyntax expression, TypeMapperControlFlowNode yes, TypeMapperControlFlowNode no)
        {
            expression = Unwrap(expression);
            if (ReferenceEquals(expression, check)) return available ? yes : no;
            if (expression is PrefixUnaryExpressionSyntax prefix)
                return Continue(prefix.Operand, no, yes);
            if (expression is not BinaryExpressionSyntax binary) return null;

            var right = buildCondition(binary.Right, yes, no);
            if (right is null) return null;
            return binary.IsKind(SyntaxKind.LogicalAndExpression)
                ? Continue(binary.Left, right, no)
                : Continue(binary.Left, yes, right);
        }

        var continuation = Continue(condition, whenTrue, whenFalse);
        if (continuation is null) return null;
        var argument = invocation.ArgumentList.Arguments.Single().Expression;
        var value = available ? previousValue : "default";

        if (argument is DeclarationExpressionSyntax declaration)
        {
            if (declaration.Designation is DiscardDesignationSyntax) return continuation;
            if (declaration.Designation is not SingleVariableDesignationSyntax local) return null;
            var name = local.Identifier.ValueText;
            if (!available && !DeclarativeControlFlowLowerer.UsesIdentifier(continuation, name))
                return continuation;

            return continuation with
            {
                Locals = ImmutableArray.Create(new TypeMapperLocalValueModel(
                    available && check.ArgumentList.Arguments[0].Expression is
                        DeclarationExpressionSyntax { Type.IsVar: true }
                        ? "var" : declaration.Type.ToString(), name, value, IsConst: false))
                    .AddRange(continuation.Locals)
            };
        }

        var sourceArgument = check.ArgumentList.Arguments.Single().Expression;
        var symbol = semanticModel.GetSymbolInfo(sourceArgument, cancellationToken).Symbol;
        if (symbol is IDiscardSymbol) return continuation;
        // Keep conditional fields/ref-return targets and their evaluation intact.
        if (argument is not IdentifierNameSyntax || symbol is not (ILocalSymbol or IParameterSymbol))
            return null;

        return new TypeMapperControlFlowNode(
            ImmutableArray<TypeMapperLocalValueModel>.Empty, Condition: null,
            WhenTrue: null, WhenFalse: null, Leaf: null, ThrowExpression: null,
            EvaluationExpression: argument + " = " + value,
            EvaluationContinuation: continuation);
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parentheses)
            expression = parentheses.Expression;
        return expression;
    }
}
