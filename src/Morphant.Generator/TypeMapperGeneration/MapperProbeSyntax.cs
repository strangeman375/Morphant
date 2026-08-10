using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class MapperProbeSyntax
{
    public static SyntaxTree Build(
        INamedTypeSymbol mapperType,
        string path,
        Action<CodeWriter> writeMembers,
        bool requiresSystemLinq = false)
    {
        var writer = new CodeWriter();

        writer.Line("#nullable enable");

        if (requiresSystemLinq)
        {
            writer.Line("using global::System.Linq;");
        }

        if (!mapperType.ContainingNamespace.IsGlobalNamespace)
        {
            writer.OpenBlock(
                "namespace " +
                mapperType.ContainingNamespace.ToDisplayString());
        }

        var containingTypes = BuildContainingTypes(mapperType);

        foreach (var containingType in containingTypes)
        {
            writer.OpenBlock(
                $"partial {GetDeclarationKind(containingType)} " +
                BuildTypeDeclarationName(containingType));
        }

        writer.OpenBlock(
            "partial class " +
            BuildTypeDeclarationName(mapperType));

        writeMembers(writer);

        writer.CloseBlock();

        for (var index = containingTypes.Length - 1;
             index >= 0;
             index--)
        {
            writer.CloseBlock();
        }

        if (!mapperType.ContainingNamespace.IsGlobalNamespace)
        {
            writer.CloseBlock();
        }

        var parseOptions = mapperType
            .DeclaringSyntaxReferences
            .First()
            .SyntaxTree
            .Options as CSharpParseOptions;

        return CSharpSyntaxTree.ParseText(
            SourceText.From(writer.ToString()),
            parseOptions,
            path);
    }

    private static ImmutableArray<INamedTypeSymbol>
        BuildContainingTypes(
            INamedTypeSymbol mapperType)
    {
        var result =
            ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        for (var containingType = mapperType.ContainingType;
             containingType is not null;
             containingType = containingType.ContainingType)
        {
            result.Add(containingType);
        }

        return result
            .ToImmutable()
            .Reverse()
            .ToImmutableArray();
    }

    private static string GetDeclarationKind(
        INamedTypeSymbol type)
    {
        if (type.IsRecord)
        {
            return type.TypeKind == TypeKind.Struct
                ? "record struct"
                : "record";
        }

        return type.TypeKind switch
        {
            TypeKind.Class => "class",
            TypeKind.Struct => "struct",
            TypeKind.Interface => "interface",
            _ => throw new InvalidOperationException(
                $"Unsupported containing type kind: {type.TypeKind}.")
        };
    }

    private static string BuildTypeDeclarationName(
        INamedTypeSymbol type)
    {
        var typeName = Identifier(type.Name);

        if (type.TypeParameters.IsEmpty)
        {
            return typeName;
        }

        return
            typeName +
            "<" +
            string.Join(
                ", ",
                type.TypeParameters.Select(
                    static typeParameter =>
                        Identifier(typeParameter.Name))) +
            ">";
    }

    private static string Identifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) !=
                   SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) !=
                   SyntaxKind.None
            ? "@" + value
            : value;
    }
}
