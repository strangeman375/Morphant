using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.MapperDeclaration;

internal static class MapperContractDisplay
{
    public static string Create(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        var result = new StringBuilder();

        AppendType(sourceType, result);
        result.Append(" -> ");
        AppendType(destinationType, result);

        return RemoveGlobalPrefixes(result.ToString());
    }

    public static string CreateType(ITypeSymbol type)
    {
        return RemoveGlobalPrefixes(type.ToDisplayString(
            SymbolDisplayFormats.FullyQualifiedNullable));
    }

    private static string RemoveGlobalPrefixes(string value) =>
        value.Replace("global::", string.Empty);

    private static void AppendType(
        ITypeSymbol type,
        StringBuilder result)
    {
        if (type is IDynamicTypeSymbol)
        {
            result.Append("object");
            return;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            AppendType(arrayType.ElementType, result);
            result.Append('[');

            if (!arrayType.IsSZArray)
            {
                result.Append(',', arrayType.Rank - 1);
            }

            result.Append(']');
            return;
        }

        if (type is ITypeParameterSymbol typeParameter)
        {
            AppendEscapedIdentifier(typeParameter.Name, result);
            return;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            result.Append(type.ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable));
            return;
        }

        namedType = Normalize(namedType);

        if (namedType.OriginalDefinition.SpecialType ==
                SpecialType.System_Nullable_T &&
            namedType.TypeArguments.Length == 1)
        {
            AppendType(namedType.TypeArguments[0], result);
            result.Append('?');
            return;
        }

        if (TryAppendSpecialType(namedType.SpecialType, result))
        {
            return;
        }

        AppendNamedType(namedType, result);
    }

    private static void AppendNamedType(
        INamedTypeSymbol type,
        StringBuilder result)
    {
        if (type.ContainingType is { } containingType)
        {
            AppendNamedType(Normalize(containingType), result);
            result.Append('.');
        }
        else
        {
            result.Append("global::");

            if (!type.ContainingNamespace.IsGlobalNamespace)
            {
                AppendNamespace(type.ContainingNamespace, result);
                result.Append('.');
            }
        }

        AppendEscapedIdentifier(type.Name, result);

        if (type.TypeArguments.IsDefaultOrEmpty)
        {
            return;
        }

        result.Append('<');

        for (var index = 0; index < type.TypeArguments.Length; index++)
        {
            if (index > 0)
            {
                result.Append(", ");
            }

            AppendType(type.TypeArguments[index], result);
        }

        result.Append('>');
    }

    private static void AppendNamespace(
        INamespaceSymbol namespaceSymbol,
        StringBuilder result)
    {
        if (!namespaceSymbol.ContainingNamespace.IsGlobalNamespace)
        {
            AppendNamespace(namespaceSymbol.ContainingNamespace, result);
            result.Append('.');
        }

        AppendEscapedIdentifier(namespaceSymbol.Name, result);
    }

    private static void AppendEscapedIdentifier(
        string identifier,
        StringBuilder result)
    {
        if (SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None ||
            SyntaxFacts.GetContextualKeywordKind(identifier) != SyntaxKind.None)
        {
            result.Append('@');
        }

        result.Append(identifier);
    }

    private static INamedTypeSymbol Normalize(INamedTypeSymbol type)
    {
        if (type.IsTupleType &&
            type.TupleUnderlyingType is { } tupleUnderlyingType)
        {
            type = tupleUnderlyingType;
        }

        if (type.IsNativeIntegerType &&
            type.NativeIntegerUnderlyingType is
                { } nativeIntegerUnderlyingType)
        {
            type = nativeIntegerUnderlyingType;
        }

        return type;
    }

    private static bool TryAppendSpecialType(
        SpecialType specialType,
        StringBuilder result)
    {
        var keyword = specialType switch
        {
            SpecialType.System_Object => "object",
            SpecialType.System_Void => "void",
            SpecialType.System_Boolean => "bool",
            SpecialType.System_Char => "char",
            SpecialType.System_SByte => "sbyte",
            SpecialType.System_Byte => "byte",
            SpecialType.System_Int16 => "short",
            SpecialType.System_UInt16 => "ushort",
            SpecialType.System_Int32 => "int",
            SpecialType.System_UInt32 => "uint",
            SpecialType.System_Int64 => "long",
            SpecialType.System_UInt64 => "ulong",
            SpecialType.System_Decimal => "decimal",
            SpecialType.System_Single => "float",
            SpecialType.System_Double => "double",
            SpecialType.System_String => "string",
            _ => null
        };

        if (keyword is null)
        {
            return false;
        }

        result.Append(keyword);
        return true;
    }
}
