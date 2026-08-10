using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DeclarativeQueryExpressionPolicy
{
    private const string SystemLinqNamespace = "System.Linq";

    public static bool IsSupported(
        SyntaxNode syntax,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var query in syntax.DescendantNodesAndSelf()
                     .OfType<QueryExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (semanticModel.GetOperation(
                    query,
                    cancellationToken) is not
                ITranslatedQueryOperation translatedQuery)
            {
                return false;
            }

            foreach (var invocation in translatedQuery.Operation
                         .DescendantsAndSelf()
                         .OfType<IInvocationOperation>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (invocation.Syntax is InvocationExpressionSyntax ||
                    !query.FullSpan.Contains(
                        invocation.Syntax.FullSpan))
                {
                    continue;
                }

                var method = invocation.TargetMethod.ReducedFrom ??
                             invocation.TargetMethod;

                if (method.IsExtensionMethod &&
                    !StringComparer.Ordinal.Equals(
                        method.ContainingNamespace.ToDisplayString(),
                        SystemLinqNamespace))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
