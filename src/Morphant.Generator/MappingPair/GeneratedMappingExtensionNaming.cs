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

        if (!IsContainer(definition.ContainingType) ||
            !HasGeneratedSignature(definition))
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

    private static bool HasGeneratedSignature(IMethodSymbol method)
    {
        if (method.MethodKind != MethodKind.Ordinary ||
            method.DeclaredAccessibility != Accessibility.Public ||
            !method.IsStatic ||
            !method.IsExtensionMethod ||
            method.Parameters.Length != 2 ||
            method.Parameters.Any(static parameter =>
                parameter.RefKind != RefKind.None ||
                parameter.IsOptional ||
                parameter.HasExplicitDefaultValue) ||
            method.ReturnType is not INamedTypeSymbol returnBuilder ||
            !HasMetadataName(returnBuilder, MetadataNames.PairMapperBuilder) ||
            !TryGetReceiverBuilder(
                method.Parameters[0].Type,
                out var receiverBuilder) ||
            !SymbolEqualityComparer.Default.Equals(
                returnBuilder,
                receiverBuilder) ||
            method.Parameters[1].Type is not INamedTypeSymbol callback ||
            callback.TypeKind != TypeKind.Delegate)
        {
            return false;
        }

        var callbackMetadataName =
            SymbolNameHelper.GetFullMetadataName(
                callback.OriginalDefinition);

        return method.Name switch
        {
            "Construct" => callbackMetadataName is
                "Morphant.Delegates.Construct`2" or
                "Morphant.Delegates.Construct`3",
            "Resolve" => callbackMetadataName is
                "Morphant.Delegates.Resolve`3" or
                "Morphant.Delegates.Resolve`4",
            "ConstructUsing" => callbackMetadataName is
                "Morphant.Delegates.ConstructUsing`2" or
                "Morphant.Delegates.ConstructUsing`3",
            "ResolveUsing" => callbackMetadataName is
                "Morphant.Delegates.ResolveUsing`3" or
                "Morphant.Delegates.ResolveUsing`4",
            "Convert" => callbackMetadataName is
                "Morphant.Delegates.Convert`2" or
                "Morphant.Delegates.Convert`3" or
                "Morphant.Delegates.Convert`4",
            "Members" => callbackMetadataName is
                "Morphant.Delegates.Members`2" or
                "Morphant.Delegates.Members`3" or
                "Morphant.Delegates.Members`4" or
                "Morphant.Delegates.Members`5",
            _ => false
        };
    }

    private static bool TryGetReceiverBuilder(
        ITypeSymbol receiver,
        out INamedTypeSymbol builder)
    {
        if (receiver is INamedTypeSymbol namedReceiver)
        {
            if (HasMetadataName(
                    namedReceiver,
                    MetadataNames.PairMapperBuilder))
            {
                builder = namedReceiver;
                return true;
            }

            if (HasMetadataName(
                    namedReceiver,
                    MetadataNames.MapperBuilderBase) &&
                namedReceiver.TypeArguments[0] is
                    INamedTypeSymbol candidate &&
                HasMetadataName(
                    candidate,
                    MetadataNames.PairMapperBuilder))
            {
                builder = candidate;
                return true;
            }
        }

        builder = null!;
        return false;
    }

    private static bool HasMetadataName(
        INamedTypeSymbol type,
        string metadataName)
    {
        return StringComparer.Ordinal.Equals(
            SymbolNameHelper.GetFullMetadataName(
                type.OriginalDefinition),
            metadataName);
    }
}
