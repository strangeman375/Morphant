using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DeclarativeContextUsagePolicy
{
    private const string MarkerMetadataName =
        "Morphant.Context.MappingContextMarker";

    public static bool IsSupported(
        SyntaxNode scope,
        IParameterSymbol? contextParameter,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (contextParameter is null)
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

            if (!SymbolEqualityComparer.Default.Equals(
                    reference.Parameter,
                    contextParameter))
            {
                continue;
            }

            IOperation current = reference;

            while (current.Parent is IConversionOperation
                       { IsImplicit: true } conversion ||
                   current.Parent is IParenthesizedOperation)
            {
                current = current.Parent!;
            }

            if (current.Parent is not IPropertyReferenceOperation property ||
                !ReferenceEquals(property.Instance, current) ||
                property.Property.Name != "Operation" ||
                SymbolNameHelper.GetFullMetadataName(
                    property.Property.ContainingType) != MarkerMetadataName)
            {
                return false;
            }
        }

        return true;
    }
}
