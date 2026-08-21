using Microsoft.CodeAnalysis;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class PolymorphicBasePlanReachability
{
    public static bool IsReachable(TypeMapperMappingModel mapping)
    {
        if (!RequiresDispatch(mapping) ||
            mapping.EffectiveSettings.UnknownDerivedTypeHandling !=
                UnknownDerivedTypeHandlingValue.Throw)
        {
            return true;
        }

        return CanHaveExactRuntimeType(
            mapping.AnalysisContext.Registration.SourceType);
    }

    public static bool RequiresDispatch(TypeMapperMappingModel mapping)
    {
        return !mapping.DerivedMappings.IsDefaultOrEmpty ||
               mapping.EffectiveSettings.UnknownDerivedTypeHandling ==
                   UnknownDerivedTypeHandlingValue.Throw;
    }

    public static bool CanHaveExactRuntimeType(ITypeSymbol sourceType)
    {
        if (sourceType is INamedTypeSymbol nullable &&
            nullable.OriginalDefinition.SpecialType ==
                SpecialType.System_Nullable_T)
        {
            sourceType = nullable.TypeArguments[0];
        }

        return sourceType.TypeKind != TypeKind.Interface &&
               sourceType is not INamedTypeSymbol { IsAbstract: true };
    }
}
