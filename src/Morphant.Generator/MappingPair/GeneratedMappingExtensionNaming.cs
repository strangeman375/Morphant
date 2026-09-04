using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class GeneratedMappingExtensionNaming
{
    private const string MappingExtensionHintPrefix =
        "Morphant.Generated.MappingExtension.";

    private const string MemberExtensionHintPrefix =
        "Morphant.Generated.MemberExtension.";

    public const string CommonContainerTypeName =
        "MorphantGeneratedMappingExtensions";

    private const string FamilyContainerTypeNamePrefix =
        CommonContainerTypeName + "__Family_";

    public static string BuildContainerTypeName(
        MappingSurfaceModel surface)
    {
        // A bare CRTP self parameter leaves the declaring family only in
        // generic constraints. Constraints are not part of a C# method
        // signature, so unrelated families need distinct declaring types.
        if (surface.Kind != MappingSurfaceKind.MapperFamilyScoped ||
            surface.MapperSelfType is not ITypeParameterSymbol)
        {
            return CommonContainerTypeName;
        }

        var familyDefinition =
            surface.DeclaringMapperType.OriginalDefinition;
        var familyIdentity =
            SymbolNameHelper.GetFullMetadataName(familyDefinition);

        return FamilyContainerTypeNamePrefix +
               HintNameHelper.GetStableHash128(familyIdentity);
    }

    public static bool IsContainer(INamedTypeSymbol type)
    {
        if (type.Arity != 0 ||
            type.ContainingType is not null ||
            !StringComparer.Ordinal.Equals(
                type.ContainingNamespace.ToDisplayString(),
                "Morphant"))
        {
            return false;
        }

        if (StringComparer.Ordinal.Equals(
                type.Name,
                CommonContainerTypeName))
        {
            return true;
        }

        if (!type.Name.StartsWith(
                FamilyContainerTypeNamePrefix,
                StringComparison.Ordinal) ||
            type.Name.Length != FamilyContainerTypeNamePrefix.Length + 32)
        {
            return false;
        }

        for (var index = FamilyContainerTypeNamePrefix.Length;
             index < type.Name.Length;
             index++)
        {
            var character = type.Name[index];

            if (character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsGeneratedMethod(IMethodSymbol method)
    {
        var definition = method.ReducedFrom ?? method;

        if (!IsContainer(definition.ContainingType))
        {
            return false;
        }

        return definition.DeclaringSyntaxReferences.Any(reference =>
        {
            var fileName = Path.GetFileName(
                reference.SyntaxTree.FilePath);

            return fileName.EndsWith(
                       ".g.cs",
                       StringComparison.Ordinal) &&
                   (fileName.StartsWith(
                        MappingExtensionHintPrefix,
                        StringComparison.Ordinal) ||
                    fileName.StartsWith(
                        MemberExtensionHintPrefix,
                        StringComparison.Ordinal));
        });
    }
}
