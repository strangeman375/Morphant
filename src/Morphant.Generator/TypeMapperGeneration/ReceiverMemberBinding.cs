using Microsoft.CodeAnalysis;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ReceiverMemberBinding
{
    public static ISymbol SubstituteMember(
        ISymbol member,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol> substitutions,
        Compilation compilation)
    {
        if (member.ContainingType is not { } declaringType)
            return member;

        var closedType = (INamedTypeSymbol)MapperTypeSubstitution.Substitute(
            declaringType, substitutions, compilation);
        return closedType.GetMembers(member.Name).FirstOrDefault(candidate =>
            SymbolEqualityComparer.Default.Equals(
                candidate.OriginalDefinition, member.OriginalDefinition)) ?? member;
    }

    public static INamedTypeSymbol? GetRequiredReceiverType(
        ITypeSymbol receiverType, ISymbol selectedMember)
    {
        if (receiverType is not INamedTypeSymbol receiver ||
            selectedMember.IsStatic ||
            selectedMember.ContainingType is not { } declaringType ||
            selectedMember is not (IPropertySymbol or IFieldSymbol or IMethodSymbol))
            return null;

        for (var current = receiver; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, declaringType))
                return null;

            foreach (var candidate in current.GetMembers(selectedMember.Name))
            {
                if (!KeepsSelectedMember(candidate, selectedMember))
                {
                    // A direct call to the unique implicit interface implementation
                    // retains constrained value-type dispatch without boxing.
                    if (receiver.IsValueType && declaringType.TypeKind == TypeKind.Interface &&
                        current.GetMembers(selectedMember.Name).Length == 1 &&
                        SymbolEqualityComparer.Default.Equals(candidate,
                            receiver.FindImplementationForInterfaceMember(selectedMember)))
                        return null;

                    return declaringType;
                }
            }
        }

        return declaringType.TypeKind == TypeKind.Interface ? declaringType : null;
    }

    private static bool KeepsSelectedMember(ISymbol candidate, ISymbol selected)
    {
        if (SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, selected.OriginalDefinition))
            return true;

        if (!SymbolEqualityComparer.IncludeNullability.Equals(ResultType(candidate), ResultType(selected)))
            return false;

        for (var overridden = GetOverridden(candidate); overridden is not null; overridden = GetOverridden(overridden))
            if (SymbolEqualityComparer.Default.Equals(overridden.OriginalDefinition, selected.OriginalDefinition))
                return true;

        return false;
    }

    private static ISymbol? GetOverridden(ISymbol member) => member switch
    {
        IPropertySymbol property => property.OverriddenProperty,
        IMethodSymbol method => method.OverriddenMethod,
        _ => null
    };

    private static ITypeSymbol? ResultType(ISymbol member) => member switch
    {
        IPropertySymbol property => property.Type,
        IFieldSymbol field => field.Type,
        IMethodSymbol method => method.ReturnType,
        _ => null
    };
}
