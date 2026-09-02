using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal enum BclTupleKind
{
    ValueTuple,
    SystemTuple
}

internal sealed record BclTupleShape(
    BclTupleKind Kind,
    INamedTypeSymbol Type,
    ImmutableArray<BclTupleElement> Elements)
{
    public bool IsValueTuple => Kind == BclTupleKind.ValueTuple;
}

internal sealed record BclTupleElement(
    int Ordinal,
    ITypeSymbol Type,
    ISymbol Symbol,
    string? SemanticName,
    string TechnicalName,
    string AccessPath)
{
    public string Name => SemanticName ?? TechnicalName;

    public bool HasSemanticName => SemanticName is not null;
}

internal static class BclTupleShapePolicy
{
    public static BclTupleShape? TryCreate(ITypeSymbol type)
    {
        type = UnwrapNullable(type);

        if (type is not INamedTypeSymbol namedType)
        {
            return null;
        }

        if (TryCreateValueTuple(namedType, out var valueTuple))
        {
            return valueTuple;
        }

        return TryCreateSystemTuple(namedType, out var systemTuple)
            ? systemTuple
            : null;
    }

    public static string BuildPresentationKey(ITypeSymbol type)
    {
        var result = new StringBuilder();
        var typeParameters =
            new Dictionary<ITypeParameterSymbol, int>(
                TypeParameterComparer.Instance);
        AppendPresentation(type, result, typeParameters);
        return result.ToString();
    }

    public static string BuildPairPresentationKey(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType)
    {
        var result = new StringBuilder();
        var typeParameters =
            new Dictionary<ITypeParameterSymbol, int>(
                TypeParameterComparer.Instance);

        AppendPresentation(sourceType, result, typeParameters);
        result.Append("->");
        AppendPresentation(destinationType, result, typeParameters);

        return result.ToString();
    }

    public static bool ContainsTuplePresentation(ITypeSymbol type)
    {
        if (TryCreate(type) is not null)
        {
            return true;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return ContainsTuplePresentation(arrayType.ElementType);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        if (namedType.ContainingType is { } containingType &&
            ContainsTuplePresentation(containingType))
        {
            return true;
        }

        return namedType.TypeArguments.Any(ContainsTuplePresentation);
    }

    public static bool AreSameLogicalElement(
        ISymbol left,
        ISymbol right)
    {
        if (SymbolEqualityComparer.Default.Equals(left, right))
        {
            return true;
        }

        return TryGetTupleElementIdentity(left, out var leftIdentity) &&
               TryGetTupleElementIdentity(right, out var rightIdentity) &&
               leftIdentity == rightIdentity;
    }

    public static BclTupleElement? FindElement(
        ITypeSymbol tupleType,
        ISymbol member)
    {
        return TryCreate(tupleType)?.Elements.FirstOrDefault(candidate =>
            AreSameLogicalElement(candidate.Symbol, member) ||
            StringComparer.Ordinal.Equals(candidate.Name, member.Name));
    }

    private static bool TryCreateValueTuple(
        INamedTypeSymbol type,
        out BclTupleShape shape)
    {
        var metadataName = GetDefinitionMetadataName(type);

        if (metadataName == "System.ValueTuple")
        {
            shape = new BclTupleShape(
                BclTupleKind.ValueTuple,
                type,
                ImmutableArray<BclTupleElement>.Empty);
            return true;
        }

        if (!IsValueTupleDefinition(metadataName, type.Arity))
        {
            shape = null!;
            return false;
        }

        var storageElements = ImmutableArray.CreateBuilder<StorageElement>();
        var storageType = type.IsTupleType
            ? type.TupleUnderlyingType ?? type
            : type;

        if (!TryFlattenValueTuple(
                storageType,
                prefix: string.Empty,
                storageElements))
        {
            shape = null!;
            return false;
        }

        var tupleElements = type.IsTupleType
            ? type.TupleElements
            : ImmutableArray<IFieldSymbol>.Empty;
        var result = ImmutableArray.CreateBuilder<BclTupleElement>(
            storageElements.Count);

        for (var index = 0; index < storageElements.Count; index++)
        {
            var storage = storageElements[index];
            var tupleElement = tupleElements.Length == storageElements.Count
                ? tupleElements[index]
                : null;
            var semanticName = tupleElement is
                { IsExplicitlyNamedTupleElement: true }
                ? tupleElement.Name
                : null;

            result.Add(new BclTupleElement(
                index + 1,
                tupleElement?.Type ?? GetMemberType(storage.Symbol),
                tupleElement ?? storage.Symbol,
                semanticName,
                "Item" + (index + 1).ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                storage.AccessPath));
        }

        shape = new BclTupleShape(
            BclTupleKind.ValueTuple,
            type,
            result.ToImmutable());
        return true;
    }

    private static bool TryFlattenValueTuple(
        INamedTypeSymbol type,
        string prefix,
        ImmutableArray<StorageElement>.Builder result)
    {
        var metadataName = GetDefinitionMetadataName(type);

        if (!IsValueTupleDefinition(metadataName, type.Arity) ||
            type.Arity == 0)
        {
            return false;
        }

        var directCount = Math.Min(type.Arity, 7);

        for (var index = 0; index < directCount; index++)
        {
            if (FindField(type, "Item" + (index + 1)) is not
                    { } field)
            {
                return false;
            }

            result.Add(new StorageElement(
                field,
                prefix + field.Name));
        }

        if (type.Arity != 8)
        {
            return true;
        }

        if (FindField(type, "Rest") is not { } rest ||
            type.TypeArguments[7] is not INamedTypeSymbol restType)
        {
            return false;
        }

        var previousCount = result.Count;

        return TryFlattenValueTuple(
                   restType,
                   prefix + rest.Name + ".",
                   result) &&
               result.Count > previousCount;
    }

    private static bool TryCreateSystemTuple(
        INamedTypeSymbol type,
        out BclTupleShape shape)
    {
        var metadataName = GetDefinitionMetadataName(type);

        if (!IsSystemTupleDefinition(metadataName, type.Arity))
        {
            shape = null!;
            return false;
        }

        var storageElements = ImmutableArray.CreateBuilder<StorageElement>();

        if (!TryFlattenSystemTuple(
                type,
                prefix: string.Empty,
                storageElements))
        {
            shape = null!;
            return false;
        }

        var result = storageElements
            .Select((element, index) => new BclTupleElement(
                index + 1,
                GetMemberType(element.Symbol),
                element.Symbol,
                SemanticName: null,
                "Item" + (index + 1).ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                element.AccessPath))
            .ToImmutableArray();

        shape = new BclTupleShape(
            BclTupleKind.SystemTuple,
            type,
            result);
        return true;
    }

    private static bool TryFlattenSystemTuple(
        INamedTypeSymbol type,
        string prefix,
        ImmutableArray<StorageElement>.Builder result)
    {
        var metadataName = GetDefinitionMetadataName(type);

        if (!IsSystemTupleDefinition(metadataName, type.Arity))
        {
            return false;
        }

        var directCount = Math.Min(type.Arity, 7);

        for (var index = 0; index < directCount; index++)
        {
            if (FindProperty(type, "Item" + (index + 1)) is not
                    { } property)
            {
                return false;
            }

            result.Add(new StorageElement(
                property,
                prefix + property.Name));
        }

        if (type.Arity != 8)
        {
            return true;
        }

        if (FindProperty(type, "Rest") is not { } rest ||
            type.TypeArguments[7] is not INamedTypeSymbol restType)
        {
            return false;
        }

        var previousCount = result.Count;

        return TryFlattenSystemTuple(
                   restType,
                   prefix + rest.Name + ".",
                   result) &&
               result.Count > previousCount;
    }

    private static void AppendPresentation(
        ITypeSymbol type,
        StringBuilder result,
        Dictionary<ITypeParameterSymbol, int> typeParameters)
    {
        result.Append(type.NullableAnnotation switch
        {
            NullableAnnotation.Annotated => '?',
            _ => '!'
        });

        if (type is IDynamicTypeSymbol)
        {
            result.Append('D');
            return;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            result.Append("A").Append(arrayType.Rank).Append('[');
            AppendPresentation(
                arrayType.ElementType,
                result,
                typeParameters);
            result.Append(']');
            return;
        }

        if (TryCreate(type) is { } tuple)
        {
            result.Append(
                    tuple.Kind == BclTupleKind.ValueTuple ? 'V' : 'T')
                .Append(tuple.Elements.Length)
                .Append('[');

            foreach (var element in tuple.Elements)
            {
                AppendLengthPrefixed(
                    result,
                    element.SemanticName ?? string.Empty);
                AppendPresentation(
                    element.Type,
                    result,
                    typeParameters);
            }

            result.Append(']');
            return;
        }

        if (type is ITypeParameterSymbol typeParameter)
        {
            if (!typeParameters.TryGetValue(typeParameter, out var ordinal))
            {
                ordinal = typeParameters.Count;
                typeParameters.Add(typeParameter, ordinal);
            }

            result.Append('P').Append(ordinal).Append(';');
            return;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            result.Append('_');
            return;
        }

        result.Append('N').Append(namedType.Arity).Append('[');

        if (namedType.ContainingType is { } containingType)
        {
            AppendPresentation(
                containingType,
                result,
                typeParameters);
        }

        foreach (var typeArgument in namedType.TypeArguments)
        {
            AppendPresentation(
                typeArgument,
                result,
                typeParameters);
        }

        result.Append(']');
    }

    private static void AppendLengthPrefixed(
        StringBuilder result,
        string value)
    {
        result.Append(value.Length)
            .Append(':')
            .Append(value)
            .Append(';');
    }

    private static bool TryGetTupleElementIdentity(
        ISymbol symbol,
        out TupleElementIdentity identity)
    {
        if (symbol is not IFieldSymbol
            {
                ContainingType.IsTupleType: true
            } field)
        {
            identity = default;
            return false;
        }

        var technicalName = field.CorrespondingTupleField?.Name ??
            field.Name;

        if (!technicalName.StartsWith(
                "Item",
                StringComparison.Ordinal) ||
            !Int32.TryParse(
                technicalName.Substring("Item".Length),
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var ordinal) ||
            ordinal <= 0)
        {
            identity = default;
            return false;
        }

        identity = new TupleElementIdentity(
            MappingTypeIdentityPolicy.Create(field.ContainingType).Key,
            ordinal);
        return true;
    }

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType &&
               namedType.OriginalDefinition.SpecialType ==
                   SpecialType.System_Nullable_T
            ? namedType.TypeArguments[0]
            : type;
    }

    private static bool IsValueTupleDefinition(
        string metadataName,
        int arity)
    {
        return arity is >= 1 and <= 8 &&
               metadataName == "System.ValueTuple`" + arity;
    }

    private static bool IsSystemTupleDefinition(
        string metadataName,
        int arity)
    {
        return arity is >= 1 and <= 8 &&
               metadataName == "System.Tuple`" + arity;
    }

    private static string GetDefinitionMetadataName(INamedTypeSymbol type)
    {
        return SymbolNameHelper.GetFullMetadataName(
            type.OriginalDefinition);
    }

    private static IFieldSymbol? FindField(
        INamedTypeSymbol type,
        string name)
    {
        return type.GetMembers(name)
            .OfType<IFieldSymbol>()
            .FirstOrDefault(static field => !field.IsStatic);
    }

    private static IPropertySymbol? FindProperty(
        INamedTypeSymbol type,
        string name)
    {
        return type.GetMembers(name)
            .OfType<IPropertySymbol>()
            .FirstOrDefault(static property => !property.IsStatic);
    }

    private static ITypeSymbol GetMemberType(ISymbol member)
    {
        return member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol property => property.Type,
            _ => throw new InvalidOperationException(
                "A tuple storage member must have a value type.")
        };
    }

    private readonly record struct StorageElement(
        ISymbol Symbol,
        string AccessPath);

    private readonly record struct TupleElementIdentity(
        string TupleTypeKey,
        int Ordinal);

    private sealed class TypeParameterComparer :
        IEqualityComparer<ITypeParameterSymbol>
    {
        public static TypeParameterComparer Instance { get; } = new();

        public bool Equals(
            ITypeParameterSymbol? left,
            ITypeParameterSymbol? right)
        {
            return SymbolEqualityComparer.Default.Equals(left, right);
        }

        public int GetHashCode(ITypeParameterSymbol typeParameter)
        {
            return SymbolEqualityComparer.Default.GetHashCode(typeParameter);
        }
    }
}
