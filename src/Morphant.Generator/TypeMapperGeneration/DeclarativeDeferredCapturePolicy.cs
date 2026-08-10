using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DeclarativeDeferredCapturePolicy
{
    public static bool IsSupported(
        SyntaxNode scope,
        IParameterSymbol? previousParameter,
        IParameterSymbol? resultParameter,
        IParameterSymbol? contextParameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (previousParameter is null &&
            resultParameter is null &&
            contextParameter is null)
        {
            return true;
        }

        var operation = semanticModel.GetOperation(
            scope,
            cancellationToken);

        if (operation is null)
        {
            return false;
        }

        foreach (var reference in operation.DescendantsAndSelf()
                     .OfType<IParameterReferenceOperation>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsProtectedParameter(
                    reference.Parameter,
                    previousParameter,
                    resultParameter,
                    contextParameter))
            {
                continue;
            }

            for (var ancestor = reference.Parent;
                 ancestor is not null &&
                 !ReferenceEquals(ancestor, operation);
                 ancestor = ancestor.Parent)
            {
                if (ancestor is IAnonymousFunctionOperation or
                    ILocalFunctionOperation)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsProtectedParameter(
        IParameterSymbol parameter,
        IParameterSymbol? previousParameter,
        IParameterSymbol? resultParameter,
        IParameterSymbol? contextParameter)
    {
        return previousParameter is not null &&
                   SymbolEqualityComparer.Default.Equals(
                       parameter,
                       previousParameter) ||
               resultParameter is not null &&
                   SymbolEqualityComparer.Default.Equals(
                       parameter,
                       resultParameter) ||
               contextParameter is not null &&
                   SymbolEqualityComparer.Default.Equals(
                       parameter,
                       contextParameter);
    }
}
