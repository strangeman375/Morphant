using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.PairConfiguration;

internal static class PolymorphicTypeRelationshipPolicy
{
    public static bool IsKnown(
        ITypeSymbol left,
        ITypeSymbol right,
        CSharpCompilation compilation,
        CancellationToken cancellationToken)
    {
        var active = new HashSet<(ITypeSymbol, ITypeSymbol)>();
        return IsStable(left, right) && IsStable(right, left);

        bool IsStable(ITypeSymbol source, ITypeSymbol destination) =>
            IsAssignable(source, destination, compilation) ||
            !CouldBecomeAssignable(source, destination, compilation,
                active, cancellationToken);
    }

    private static bool CouldBecomeAssignable(
        ITypeSymbol source,
        ITypeSymbol destination,
        CSharpCompilation compilation,
        HashSet<(ITypeSymbol, ITypeSymbol)> active,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsAssignable(source, destination, compilation))
            return true;
        if (!ContainsParameter(source) && !ContainsParameter(destination))
            return false;
        if (!active.Add((source, destination)))
            return true;

        try
        {
            if (CanBecomeEqual(source, destination, compilation))
                return true;

            if (destination is ITypeParameterSymbol targetParameter)
            {
                if (source.IsReferenceType && targetParameter.HasValueTypeConstraint)
                    return false;
                return targetParameter.ConstraintTypes.All(constraint =>
                    CouldBecomeAssignable(source, constraint, compilation, active, cancellationToken));
            }

            if (source is ITypeParameterSymbol sourceParameter)
            {
                if (destination.IsValueType)
                    return destination is INamedTypeSymbol nullable &&
                        nullable.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
                        CanBecomeEqual(source, nullable.TypeArguments[0], compilation);

                if (sourceParameter.HasValueTypeConstraint &&
                    destination.TypeKind == TypeKind.Class &&
                    destination.SpecialType is not (SpecialType.System_Object or
                        SpecialType.System_ValueType or SpecialType.System_Enum))
                    return false;

                foreach (var constraint in sourceParameter.ConstraintTypes)
                {
                    if (constraint.TypeKind == TypeKind.Class && destination.TypeKind == TypeKind.Class &&
                        !CouldBecomeAssignable(constraint, destination, compilation, active, cancellationToken) &&
                        !CouldBecomeAssignable(destination, constraint, compilation, active, cancellationToken))
                        return false;
                    if (destination is INamedTypeSymbol { IsSealed: true } &&
                        !CouldBecomeAssignable(destination, constraint, compilation, active, cancellationToken))
                        return false;
                }
                return true;
            }

            if (source is IArrayTypeSymbol sourceArray && destination is IArrayTypeSymbol destinationArray)
                return sourceArray.Rank == destinationArray.Rank &&
                    sourceArray.IsSZArray == destinationArray.IsSZArray &&
                    CouldVary(sourceArray.ElementType, destinationArray.ElementType);

            if (destination is not INamedTypeSymbol target)
                return false;

            for (var current = source; current is not null; current = current.BaseType)
            {
                if (current is INamedTypeSymbol named && CouldMatch(named, target))
                    return true;
            }
            return source.AllInterfaces.Any(candidate => CouldMatch(candidate, target));
        }
        finally
        {
            active.Remove((source, destination));
        }

        bool CouldVary(ITypeSymbol from, ITypeSymbol to) =>
            CanBecomeEqual(from, to, compilation) ||
            !from.IsValueType && !to.IsValueType &&
            CouldBecomeAssignable(from, to, compilation, active, cancellationToken);

        bool CouldMatch(INamedTypeSymbol candidate, INamedTypeSymbol target)
        {
            if (!SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, target.OriginalDefinition))
                return false;
            if (candidate.ContainingType is { } containing &&
                (target.ContainingType is not { } targetContaining ||
                 !CanBecomeEqual(containing, targetContaining, compilation)))
                return false;
            if (candidate.TypeParameters.All(parameter => parameter.Variance == VarianceKind.None))
                return CanBecomeEqual(candidate, target, compilation);

            for (var index = 0; index < candidate.TypeArguments.Length; index++)
            {
                var from = candidate.TypeArguments[index];
                var to = target.TypeArguments[index];
                var matches = candidate.TypeParameters[index].Variance switch
                {
                    VarianceKind.Out => CouldVary(from, to),
                    VarianceKind.In => CouldVary(to, from),
                    _ => CanBecomeEqual(from, to, compilation)
                };
                if (!matches)
                    return false;
            }
            return true;
        }
    }

    private static bool CanBecomeEqual(
        ITypeSymbol left,
        ITypeSymbol right,
        CSharpCompilation compilation)
    {
        if (!MappingTypeIdentityPolicy.CanTypesUnify(left, right))
            return false;
        if (left is ITypeParameterSymbol leftParameter)
            return CanRepresent(leftParameter, right, compilation);
        if (right is ITypeParameterSymbol rightParameter)
            return CanRepresent(rightParameter, left, compilation);
        if (left is IArrayTypeSymbol leftArray && right is IArrayTypeSymbol rightArray)
            return CanBecomeEqual(leftArray.ElementType, rightArray.ElementType, compilation);
        if (left is INamedTypeSymbol leftNamed && right is INamedTypeSymbol rightNamed)
        {
            if (leftNamed.ContainingType is { } containing &&
                rightNamed.ContainingType is { } rightContaining &&
                !CanBecomeEqual(containing, rightContaining, compilation))
                return false;
            for (var index = 0; index < leftNamed.TypeArguments.Length; index++)
                if (!CanBecomeEqual(leftNamed.TypeArguments[index], rightNamed.TypeArguments[index], compilation))
                    return false;
        }
        return true;
    }

    private static bool CanRepresent(
        ITypeParameterSymbol parameter,
        ITypeSymbol type,
        CSharpCompilation compilation)
    {
        if (parameter.IsReferenceType && type.IsValueType ||
            parameter.HasValueTypeConstraint && (type.IsReferenceType ||
                type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T))
            return false;
        if (!ContainsParameter(type))
        {
            if (parameter.HasUnmanagedTypeConstraint && !type.IsUnmanagedType)
                return false;
            foreach (var constraint in parameter.ConstraintTypes)
                if (!ContainsParameter(constraint) && !IsAssignable(type, constraint, compilation))
                    return false;
        }
        return true;
    }

    private static bool IsAssignable(ITypeSymbol source, ITypeSymbol destination, CSharpCompilation compilation)
    {
        var conversion = compilation.ClassifyConversion(source, destination);
        return conversion.IsIdentity || conversion.IsImplicit &&
            (conversion.IsReference || conversion.IsBoxing || conversion.IsNullable);
    }

    private static bool ContainsParameter(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => ContainsParameter(array.ElementType),
        INamedTypeSymbol named =>
            named.ContainingType is { } containing && ContainsParameter(containing) ||
            named.TypeArguments.Any(ContainsParameter),
        _ => false
    };
}
