using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TransferableLambdaSyntax
{
    public static bool TryGetResult(
        LambdaExpressionSyntax lambda,
        out ImmutableArray<LocalDeclarationStatementSyntax>
            localDeclarations,
        out ExpressionSyntax resultExpression)
    {
        if (lambda.ExpressionBody is { } expressionBody)
        {
            localDeclarations = [];
            resultExpression = expressionBody;
            return true;
        }

        if (lambda.Block is not { } block ||
            !TryGetBlockResult(
                block,
                out localDeclarations,
                out resultExpression))
        {
            localDeclarations = default;
            resultExpression = null!;
            return false;
        }

        return true;
    }

    public static bool HasSupportedBody(
        LocalFunctionStatementSyntax localFunction)
    {
        if (localFunction.ExpressionBody is
            {
                Expression: not null
            })
        {
            return true;
        }

        return localFunction.Body is { } body &&
               TryGetBlockResult(
                   body,
                   out _,
                   out _);
    }

    private static bool TryGetBlockResult(
        BlockSyntax block,
        out ImmutableArray<LocalDeclarationStatementSyntax>
            localDeclarations,
        out ExpressionSyntax resultExpression)
    {
        if (block.Statements.Count == 0 ||
            block.Statements[block.Statements.Count - 1] is not
                ReturnStatementSyntax
                {
                    Expression: { } returnExpression
                })
        {
            localDeclarations = default;
            resultExpression = null!;
            return false;
        }

        var declarations =
            ImmutableArray.CreateBuilder<
                LocalDeclarationStatementSyntax>(
                block.Statements.Count - 1);

        for (var index = 0;
             index < block.Statements.Count - 1;
             index++)
        {
            if (block.Statements[index] is not
                    LocalDeclarationStatementSyntax declaration ||
                !declaration.UsingKeyword.IsKind(
                    SyntaxKind.None) ||
                declaration.Declaration.Type is
                    RefTypeSyntax ||
                declaration.Declaration.Variables.Count == 0 ||
                declaration.Declaration.Variables.Any(
                    static variable =>
                        variable.Initializer is null))
            {
                localDeclarations = default;
                resultExpression = null!;
                return false;
            }

            declarations.Add(declaration);
        }

        localDeclarations = declarations.ToImmutable();
        resultExpression = returnExpression;
        return true;
    }
}
