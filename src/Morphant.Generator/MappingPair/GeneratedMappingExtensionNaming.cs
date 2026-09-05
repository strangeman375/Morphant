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

    public static bool IsContainer(INamedTypeSymbol type)
    {
        return type.Arity == 0 &&
               type.ContainingType is null &&
               StringComparer.Ordinal.Equals(
                   type.ContainingNamespace.ToDisplayString(),
                   "Morphant") &&
               StringComparer.Ordinal.Equals(
                   type.Name,
                   CommonContainerTypeName);
    }

    public static bool IsGeneratedMethod(
        IMethodSymbol method,
        Compilation compilation)
    {
        var definition = method.ReducedFrom ?? method;

        if (!SymbolEqualityComparer.Default.Equals(
                definition.ContainingAssembly, compilation.Assembly) ||
            !IsContainer(definition.ContainingType) ||
            !HasGeneratedSignature(definition))
        {
            return false;
        }

        return definition.DeclaringSyntaxReferences.Any(reference =>
        {
            var fileName = Path.GetFileName(
                reference.SyntaxTree.FilePath);

            return compilation.ContainsSyntaxTree(reference.SyntaxTree) &&
                   fileName.EndsWith(
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
            !HasMatchingReceiver(method.Parameters[0].Type, returnBuilder) ||
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

    private static bool HasMatchingReceiver(
        ITypeSymbol receiver,
        INamedTypeSymbol returnBuilder)
    {
        if (SymbolEqualityComparer.IncludeNullability.Equals(
                receiver, returnBuilder))
        {
            return true;
        }

        return receiver is INamedTypeSymbol familyReceiver &&
               HasMetadataName(
                   familyReceiver, MetadataNames.PairMapperBuilderInterface) &&
               familyReceiver.TypeArguments[0] is INamedTypeSymbol owner &&
               SymbolEqualityComparer.Default.Equals(
                   MappingSurfacePolicy.FindMapperSelfType(owner),
                   returnBuilder.TypeArguments[0]) &&
               SymbolEqualityComparer.IncludeNullability.Equals(
                   familyReceiver.TypeArguments[1],
                   returnBuilder.TypeArguments[1]) &&
               SymbolEqualityComparer.IncludeNullability.Equals(
                   familyReceiver.TypeArguments[2],
                   returnBuilder.TypeArguments[2]);
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
