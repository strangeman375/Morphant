using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class DestinationCapabilityPolicy
{
    public static MappingPairCapabilities Build(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        if (MappingTypeEligibilityPolicy.IsDeferredOpaqueRoot(sourceType) ||
            MappingTypeEligibilityPolicy.IsDeferredOpaqueRoot(
                destinationType))
        {
            return new MappingPairCapabilities(
                Runtime: true,
                Manual: true,
                MappingConstructionKind.Direct,
                Members: false);
        }

        var destination = GetDestinationType(
            destinationType,
            compilation);

        if (BclTupleShapePolicy.TryCreate(destination) is { } tuple)
        {
            return new MappingPairCapabilities(
                Runtime: true,
                Manual: true,
                MappingConstructionKind.Structured,
                Members: !tuple.Elements.IsEmpty,
                IntrinsicConstruction: true);
        }

        var isOpaque = IsOpaque(destination);
        // Construction and member plan declarations reproduce the complete
        // destination type-parameter constraint list.
        var canGeneratePlans =
            MappingTypeEligibilityPolicy.CanCopyTypeParameterConstraints(
                destination,
                compilation);
        var hasSupportedConstructor =
            !isOpaque &&
            canGeneratePlans &&
            !GetSupportedConstructors(
                    destination,
                    compilation,
                    cancellationToken)
                .IsDefaultOrEmpty;
        var hasMembers = !isOpaque &&
            canGeneratePlans &&
            !DestinationMemberPolicy.GetSupportedMembers(
                destination,
                compilation,
                includeInitOnlyProperties: hasSupportedConstructor,
                cancellationToken: cancellationToken).IsEmpty;

        return new MappingPairCapabilities(
            Runtime: true,
            Manual: true,
            hasSupportedConstructor
                ? MappingConstructionKind.Structured
                : MappingConstructionKind.Direct,
            hasMembers);
    }

    internal static INamedTypeSymbol GetDestinationType(
        ITypeSymbol destinationType,
        Compilation compilation)
    {
        var normalized = GetNormalizedDestinationType(
            destinationType,
            compilation);

        return (INamedTypeSymbol)normalized;
    }

    internal static ITypeSymbol GetNormalizedDestinationType(
        ITypeSymbol destinationType,
        Compilation compilation)
    {
        if (destinationType is IDynamicTypeSymbol)
        {
            return compilation.GetSpecialType(
                SpecialType.System_Object);
        }

        return destinationType is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.SpecialType ==
                   SpecialType.System_Nullable_T
            ? namedType.TypeArguments[0]
            : destinationType;
    }

    internal static bool IsOpaque(
        ITypeSymbol destinationType,
        Compilation compilation)
    {
        if (MappingTypeEligibilityPolicy.IsDeferredOpaqueRoot(
                destinationType))
        {
            return true;
        }

        return GetNormalizedDestinationType(
                destinationType,
                compilation) is not INamedTypeSymbol namedType ||
            IsOpaque(namedType);
    }

    internal static bool IsOpaque(INamedTypeSymbol destinationType)
    {
        if (destinationType.TypeKind == TypeKind.Enum ||
            destinationType.SpecialType is
                SpecialType.System_Object or
                SpecialType.System_String or
                SpecialType.System_Boolean or
                SpecialType.System_Char or
                SpecialType.System_SByte or
                SpecialType.System_Byte or
                SpecialType.System_Int16 or
                SpecialType.System_UInt16 or
                SpecialType.System_Int32 or
                SpecialType.System_UInt32 or
                SpecialType.System_Int64 or
                SpecialType.System_UInt64 or
                SpecialType.System_IntPtr or
                SpecialType.System_UIntPtr or
                SpecialType.System_Single or
                SpecialType.System_Double or
                SpecialType.System_Decimal)
        {
            return true;
        }

        return SymbolNameHelper.GetFullMetadataName(
                   destinationType.OriginalDefinition) is
               "System.Guid" or
               "System.DateTime" or
               "System.DateTimeOffset" or
               "System.DateOnly" or
               "System.TimeOnly" or
               "System.TimeSpan" or
               "System.Half" or
               "System.Int128" or
               "System.UInt128" or
               "System.Uri" or
               "System.Version" or
               "System.Numerics.BigInteger" or
               "System.Numerics.Complex" or
               "System.Text.Rune" or
               "System.Index" or
               "System.Range";
    }

    internal static ImmutableArray<IMethodSymbol> GetSupportedConstructors(
        INamedTypeSymbol destinationType,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        if (destinationType.TypeKind == TypeKind.Interface ||
            destinationType.IsAbstract)
        {
            return ImmutableArray<IMethodSymbol>.Empty;
        }

        var result = ImmutableArray.CreateBuilder<IMethodSymbol>();

        foreach (var constructor in destinationType.InstanceConstructors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!compilation.IsSymbolAccessibleWithin(
                    constructor,
                    compilation.Assembly) ||
                constructor.Parameters.Any(
                    parameter =>
                        parameter.RefKind != RefKind.None ||
                        parameter.Type.IsRefLikeType ||
                        !MappingTypeEligibilityPolicy.CanBeNamed(
                            parameter.Type,
                            compilation)))
            {
                continue;
            }

            result.Add(constructor);
        }

        return result.ToImmutable();
    }
}
