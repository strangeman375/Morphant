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
