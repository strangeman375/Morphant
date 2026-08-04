using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TransferableLambdaSyntax
{
    public static bool TryGetCaptures(
        SyntaxNode transferredSyntax,
        SemanticModel semanticModel,
        HashSet<ISymbol> allowedCapturedSymbols,
        string placeholderPrefix,
        CancellationToken cancellationToken,
        out ImmutableArray<TransferableLambdaCaptureSyntax>
            captures)
    {
        var result =
            ImmutableArray.CreateBuilder<
                TransferableLambdaCaptureSyntax>();
        var seen =
            new HashSet<ISymbol>(
                SymbolEqualityComparer.Default);
        var reservedNames = BuildReservedNames(
            transferredSyntax);
        var captureOrdinal = 0;

        foreach (var identifier in transferredSyntax
                     .DescendantNodesAndSelf()
                     .OfType<SimpleNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var symbol = semanticModel.GetSymbolInfo(
                    identifier,
                    cancellationToken)
                .Symbol;

            if (symbol is ILocalSymbol
                {
                    IsConst: true
                })
            {
                continue;
            }

            if (symbol is IMethodSymbol
                {
                    MethodKind: MethodKind.LocalFunction
                })
            {
                if (!IsDeclaredWithin(
                        symbol,
                        transferredSyntax))
                {
                    captures = default;
                    return false;
                }

                continue;
            }

            if (symbol is IRangeVariableSymbol)
            {
                if (!IsDeclaredWithin(
                        symbol,
                        transferredSyntax))
                {
                    captures = default;
                    return false;
                }

                continue;
            }

            if (symbol is not ILocalSymbol &&
                symbol is not IParameterSymbol)
            {
                continue;
            }

            if (IsDeclaredWithin(
                    symbol,
                    transferredSyntax))
            {
                continue;
            }

            if (!allowedCapturedSymbols.Contains(symbol))
            {
                captures = default;
                return false;
            }

            if (!seen.Add(symbol))
            {
                continue;
            }

            var placeholder = AllocateName(
                placeholderPrefix,
                ref captureOrdinal,
                reservedNames);
            result.Add(
                new TransferableLambdaCaptureSyntax(
                    symbol,
                    placeholder,
                    symbol.Name));
        }

        captures = result.ToImmutable();
        return true;
    }

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

    private static bool IsDeclaredWithin(
        ISymbol symbol,
        SyntaxNode syntax)
    {
        foreach (var reference in
                 symbol.DeclaringSyntaxReferences)
        {
            if (ReferenceEquals(
                    reference.SyntaxTree,
                    syntax.SyntaxTree) &&
                syntax.FullSpan.Contains(
                    reference.Span))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> BuildReservedNames(
        SyntaxNode syntax)
    {
        return new HashSet<string>(
            syntax.DescendantTokens()
                .Where(static token =>
                    token.IsKind(
                        SyntaxKind.IdentifierToken))
                .Select(static token =>
                    token.ValueText),
            StringComparer.Ordinal);
    }

    private static string AllocateName(
        string prefix,
        ref int ordinal,
        HashSet<string> reservedNames)
    {
        while (true)
        {
            var candidate =
                prefix +
                ordinal++.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);

            if (reservedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }
}

internal readonly record struct TransferableLambdaCaptureSyntax(
    ISymbol Symbol,
    string PlaceholderName,
    string PreferredName);
