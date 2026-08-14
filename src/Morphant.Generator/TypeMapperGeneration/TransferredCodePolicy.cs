using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.PairConfiguration;

namespace Morphant.Generator.TypeMapperGeneration;

internal sealed class TransferredCodePolicy
{
    private readonly ImmutableArray<BoundConfigurationExpression>
        _expressions;

    private TransferredCodePolicy(
        ImmutableArray<BoundConfigurationExpression> expressions)
    {
        _expressions = expressions;
        RequiresUnsafeContext = expressions.Any(static expression =>
            RequiresUnsafeContextFor(expression));
    }

    public static TransferredCodePolicy Empty { get; } =
        new(ImmutableArray<BoundConfigurationExpression>.Empty);

    public bool HasTransferredCode => !_expressions.IsEmpty;

    public BoundConfigurationExpression? PrimaryExpression =>
        _expressions.IsEmpty ? null : _expressions[0];

    public bool RequiresUnsafeContext { get; }

    public static TransferredCodePolicy Build(
        PairConfigurationModel configuration)
    {
        var expressions = configuration.Declarative.ResultPolicies
            .Select(static policy => policy.Expression)
            .Concat(configuration.Declarative.Members.Select(
                static members => members.Expression))
            .Concat(configuration.Manual.Conversions.Select(
                static conversion => conversion.Expression))
            .ToImmutableArray();

        return expressions.IsEmpty
            ? Empty
            : new TransferredCodePolicy(expressions);
    }

    public bool CanSuppress(
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        if (diagnostic.DefaultSeverity != DiagnosticSeverity.Warning)
        {
            return false;
        }

        var isNullableWarning =
            MappingExpressionCompatibility.IsNullableWarning(
                diagnostic.Id);

        foreach (var expression in _expressions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (CanSuppress(
                    expression,
                    diagnostic.Id,
                    isNullableWarning,
                    cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsSourceOwned(
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        foreach (var expression in _expressions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceDiagnostics = expression.SemanticModel.GetDiagnostics(
                expression.Syntax.Span,
                cancellationToken);

            if (sourceDiagnostics.Any(sourceDiagnostic =>
                    sourceDiagnostic.Id == diagnostic.Id &&
                    sourceDiagnostic.Severity == diagnostic.Severity &&
                    StringComparer.Ordinal.Equals(
                        sourceDiagnostic.GetMessage(),
                        diagnostic.GetMessage())))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanSuppress(
        BoundConfigurationExpression expression,
        string diagnosticId,
        bool isNullableWarning,
        CancellationToken cancellationToken)
    {
        foreach (var position in EnumerateContextPositions(
                     expression.Syntax,
                     cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsSuppressedByPragma(
                    expression.Syntax.SyntaxTree,
                    diagnosticId,
                    position,
                    cancellationToken))
            {
                return true;
            }

            if (!isNullableWarning)
            {
                continue;
            }

            var nullableContext =
                expression.SemanticModel.GetNullableContext(position);

            if (!nullableContext.WarningsEnabled())
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSuppressedByPragma(
        SyntaxTree syntaxTree,
        string diagnosticId,
        int position,
        CancellationToken cancellationToken)
    {
        var disableAll = false;
        bool? specificState = null;

        foreach (var directive in syntaxTree.GetRoot(cancellationToken)
                     .DescendantTrivia(descendIntoTrivia: true)
                     .Where(trivia => trivia.FullSpan.Start < position)
                     .Select(static trivia => trivia.GetStructure())
                     .OfType<PragmaWarningDirectiveTriviaSyntax>()
                     .Where(static directive => directive.IsActive))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var disables = directive.DisableOrRestoreKeyword.IsKind(
                SyntaxKind.DisableKeyword);

            if (directive.ErrorCodes.Count == 0)
            {
                disableAll = disables;
                specificState = null;
                continue;
            }

            if (directive.ErrorCodes.Any(errorCode =>
                    MatchesDiagnosticId(errorCode, diagnosticId)))
            {
                specificState = disables;
            }
        }

        return specificState ?? disableAll;
    }

    private static bool MatchesDiagnosticId(
        ExpressionSyntax errorCode,
        string diagnosticId)
    {
        var value = errorCode.ToString().Trim();

        if (string.Equals(
                value,
                "nullable",
                StringComparison.OrdinalIgnoreCase) &&
            MappingExpressionCompatibility.IsNullableWarning(
                diagnosticId))
        {
            return true;
        }

        if (string.Equals(
                value,
                diagnosticId,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return int.TryParse(value, out var numericCode) &&
               string.Equals(
                   "CS" + numericCode.ToString("D4"),
                   diagnosticId,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<int> EnumerateContextPositions(
        ExpressionSyntax syntax,
        CancellationToken cancellationToken)
    {
        yield return syntax.SpanStart;

        var treeLength = syntax.SyntaxTree.GetText(cancellationToken).Length;

        foreach (var trivia in syntax.DescendantTrivia(
                     descendIntoTrivia: true))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!trivia.IsDirective)
            {
                continue;
            }

            yield return Math.Min(trivia.FullSpan.End, treeLength);
        }
    }

    internal static bool RequiresUnsafeContextFor(
        BoundConfigurationExpression expression)
    {
        return expression.Syntax.DescendantNodesAndSelf().Any(node =>
            NodeRequiresUnsafeContext(node, expression.SemanticModel) &&
            !HasTransferredUnsafeScope(
                node,
                expression.Syntax));
    }

    private static bool NodeRequiresUnsafeContext(
        SyntaxNode node,
        SemanticModel semanticModel)
    {
        if (node is PointerTypeSyntax or
            FunctionPointerTypeSyntax or
            FixedStatementSyntax ||
            node is PrefixUnaryExpressionSyntax prefix &&
            prefix.IsKind(SyntaxKind.AddressOfExpression) ||
            node is PrefixUnaryExpressionSyntax indirection &&
            indirection.IsKind(
                SyntaxKind.PointerIndirectionExpression) ||
            node is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.IsKind(
                SyntaxKind.PointerMemberAccessExpression) ||
            node is SizeOfExpressionSyntax
            {
                Type: not PredefinedTypeSyntax
            })
        {
            return true;
        }

        if (node is not (ExpressionSyntax or TypeSyntax))
        {
            return false;
        }

        var type = semanticModel.GetTypeInfo(node).Type;

        return type is IPointerTypeSymbol or
            IFunctionPointerTypeSymbol;
    }

    private static bool HasTransferredUnsafeScope(
        SyntaxNode node,
        ExpressionSyntax transferRoot)
    {
        for (var current = node.Parent;
             current is not null &&
             transferRoot.FullSpan.Contains(current.FullSpan);
             current = current.Parent)
        {
            if (current is UnsafeStatementSyntax)
            {
                return true;
            }

            var modifiers = current switch
            {
                LocalFunctionStatementSyntax function =>
                    function.Modifiers,
                AnonymousFunctionExpressionSyntax anonymous
                    when !ReferenceEquals(anonymous, transferRoot) =>
                    anonymous.Modifiers,
                _ => default
            };

            if (modifiers.Any(static modifier =>
                    modifier.IsKind(SyntaxKind.UnsafeKeyword)))
            {
                return true;
            }
        }

        return false;
    }
}
