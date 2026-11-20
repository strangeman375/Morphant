using System.Text;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class MappingTypeIdentityPolicy
{
    public static MappingTypeIdentity Create(
        ITypeSymbol type,
        IReadOnlyDictionary<ITypeParameterSymbol, string>? parameterNames = null)
    {
        var key = new StringBuilder();
        var displayName = new StringBuilder();

        Append(type, key, displayName, parameterNames);

        return new MappingTypeIdentity(
            key.ToString(),
            displayName.ToString());
    }

    public static bool AreEquivalent(
        ITypeSymbol left,
        ITypeSymbol right)
    {
        var leftTypeParameter = left as ITypeParameterSymbol;
        var rightTypeParameter = right as ITypeParameterSymbol;

        if (leftTypeParameter is not null ||
            rightTypeParameter is not null)
        {
            return leftTypeParameter is not null &&
                   rightTypeParameter is not null &&
                   SymbolEqualityComparer.Default.Equals(
                       leftTypeParameter,
                       rightTypeParameter);
        }

        if (IsObjectLike(left) || IsObjectLike(right))
        {
            return IsObjectLike(left) &&
                   IsObjectLike(right);
        }

        var leftArray = left as IArrayTypeSymbol;
        var rightArray = right as IArrayTypeSymbol;

        if (leftArray is not null || rightArray is not null)
        {
            return leftArray is not null &&
                   rightArray is not null &&
                   leftArray.Rank == rightArray.Rank &&
                   leftArray.IsSZArray == rightArray.IsSZArray &&
                   AreEquivalent(
                       leftArray.ElementType,
                       rightArray.ElementType);
        }

        if (left is not INamedTypeSymbol leftNamed ||
            right is not INamedTypeSymbol rightNamed)
        {
            return false;
        }

        leftNamed = Normalize(leftNamed);
        rightNamed = Normalize(rightNamed);

        if (!SymbolEqualityComparer.Default.Equals(
                leftNamed.OriginalDefinition,
                rightNamed.OriginalDefinition) ||
            !AreContainingTypesEquivalent(
                leftNamed.ContainingType,
                rightNamed.ContainingType) ||
            leftNamed.TypeArguments.Length !=
                rightNamed.TypeArguments.Length)
        {
            return false;
        }

        for (var index = 0;
             index < leftNamed.TypeArguments.Length;
             index++)
        {
            if (!AreEquivalent(
                    leftNamed.TypeArguments[index],
                    rightNamed.TypeArguments[index]))
            {
                return false;
            }
        }

        return true;
    }

    public static string CreateAlphaEquivalentPairKey(
        ITypeSymbol source,
        ITypeSymbol destination)
    {
        var result = new StringBuilder();
        var typeParameters =
            new Dictionary<ITypeParameterSymbol, int>(
                TypeParameterComparer.Instance);

        AppendAlphaEquivalentKey(
            source,
            result,
            typeParameters);
        result.Append("->");
        AppendAlphaEquivalentKey(
            destination,
            result,
            typeParameters);

        return result.ToString();
    }

    public static bool CanTypesUnify(ITypeSymbol left, ITypeSymbol right) =>
        new Unifier().Match(new(left, false), new(right, false));

    public static bool CanPairsUnify(
        ITypeSymbol leftSource, ITypeSymbol leftDestination,
        ITypeSymbol rightSource, ITypeSymbol rightDestination)
    {
        var unifier = new Unifier();
        return unifier.Match(new(leftSource, false), new(rightSource, false)) &&
               unifier.Match(new(leftDestination, false), new(rightDestination, false));
    }

    public static bool ExtensionsCanBeAmbiguous(MappingPairModel left, MappingPairModel right,
        Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation, CancellationToken cancellationToken)
    {
        // Each extension declares its own method type parameters, even when
        // both registrations refer to the same mapper or containing type.
        var unifier = new Unifier();
        if (!unifier.Match(new(left.SourceType, false), new(right.SourceType, true)) ||
            !unifier.Match(new(left.DestinationType, false), new(right.DestinationType, true)))
            return false;

        // C# compares positions in the unsubstituted parameter types. Reusing
        // a type parameter restricts applicability but does not break a tie.
        if (IsMoreSpecificExtension(left, right) || IsMoreSpecificExtension(right, left))
            return false;
        if (!OpenExtensionConstraintCheck.HasConstraints(left.SourceType) &&
            !OpenExtensionConstraintCheck.HasConstraints(left.DestinationType) &&
            !OpenExtensionConstraintCheck.HasConstraints(right.SourceType) &&
            !OpenExtensionConstraintCheck.HasConstraints(right.DestinationType))
            return true;

        var parameters = new Dictionary<TypeTerm, string>();
        var source = unifier.Format(new(left.SourceType, false), parameters);
        var destination = unifier.Format(new(left.DestinationType, false), parameters);
        return OpenExtensionConstraintCheck.CanConstruct(source, destination, parameters.Values, compilation, cancellationToken);
    }

    internal static bool IsMoreSpecificExtension(MappingPairModel left, MappingPairModel right) =>
        (HasMoreSpecificPosition(left.SourceType, right.SourceType) ||
         HasMoreSpecificPosition(left.DestinationType, right.DestinationType)) &&
        !HasMoreSpecificPosition(right.SourceType, left.SourceType) &&
        !HasMoreSpecificPosition(right.DestinationType, left.DestinationType);

    private static bool HasMoreSpecificPosition(ITypeSymbol left, ITypeSymbol right)
    {
        if (right is ITypeParameterSymbol)
            return left is not ITypeParameterSymbol;

        if (left is IArrayTypeSymbol leftArray && right is IArrayTypeSymbol rightArray)
            return HasMoreSpecificPosition(leftArray.ElementType, rightArray.ElementType);

        if (left is not INamedTypeSymbol leftNamed || right is not INamedTypeSymbol rightNamed)
            return false;

        leftNamed = Normalize(leftNamed);
        rightNamed = Normalize(rightNamed);
        if (leftNamed.ContainingType is { } leftContainer && rightNamed.ContainingType is { } rightContainer &&
            HasMoreSpecificPosition(leftContainer, rightContainer))
            return true;

        for (var i = 0; i < leftNamed.TypeArguments.Length; i++)
        {
            if (HasMoreSpecificPosition(leftNamed.TypeArguments[i], rightNamed.TypeArguments[i]))
                return true;
        }

        return false;
    }

    private static void Append(
        ITypeSymbol type,
        StringBuilder key,
        StringBuilder displayName,
        IReadOnlyDictionary<ITypeParameterSymbol, string>? parameterNames)
    {
        if (IsObjectLike(type))
        {
            key.Append("special:System.Object");
            displayName.Append("global::System.Object");
            return;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            key.Append("array(");
            Append(arrayType.ElementType, key, displayName, parameterNames);
            key.Append(';')
                .Append(arrayType.Rank)
                .Append(';')
                .Append(arrayType.IsSZArray)
                .Append(')');

            displayName.Append('[');

            if (!arrayType.IsSZArray)
            {
                displayName.Append(',', arrayType.Rank - 1);
            }

            displayName.Append(']');
            return;
        }

        if (type is ITypeParameterSymbol typeParameter)
        {
            key.Append("parameter:")
                .Append(GetContainingSymbolIdentity(
                    typeParameter.ContainingSymbol))
                .Append(':')
                .Append((int)typeParameter.TypeParameterKind)
                .Append(':')
                .Append(typeParameter.Ordinal);
            displayName.Append(parameterNames is null ? typeParameter.Name : parameterNames[typeParameter]);
            return;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            key.Append(type.Kind)
                .Append(':')
                .Append(type.ToDisplayString());
            displayName.Append(type.ToDisplayString());
            return;
        }

        AppendNamed(
            Normalize(namedType),
            key,
            displayName,
            parameterNames);
    }

    private static void AppendAlphaEquivalentKey(
        ITypeSymbol type,
        StringBuilder result,
        Dictionary<ITypeParameterSymbol, int> typeParameters)
    {
        if (IsObjectLike(type))
        {
            result.Append("special:System.Object");
            return;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            result.Append("array(");
            AppendAlphaEquivalentKey(
                arrayType.ElementType,
                result,
                typeParameters);
            result.Append(';')
                .Append(arrayType.Rank)
                .Append(';')
                .Append(arrayType.IsSZArray)
                .Append(')');
            return;
        }

        if (type is ITypeParameterSymbol typeParameter)
        {
            if (!typeParameters.TryGetValue(
                    typeParameter,
                    out var ordinal))
            {
                ordinal = typeParameters.Count;
                typeParameters.Add(typeParameter, ordinal);
            }

            result.Append("parameter:").Append(ordinal);
            return;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            result.Append(type.Kind)
                .Append(':')
                .Append(type.ToDisplayString());
            return;
        }

        AppendAlphaEquivalentNamedKey(
            Normalize(namedType),
            result,
            typeParameters);
    }

    private static void AppendAlphaEquivalentNamedKey(
        INamedTypeSymbol type,
        StringBuilder result,
        Dictionary<ITypeParameterSymbol, int> typeParameters)
    {
        if (type.ContainingType is { } containingType)
        {
            AppendAlphaEquivalentNamedKey(
                Normalize(containingType),
                result,
                typeParameters);
            result.Append('+');
        }
        else
        {
            result.Append("named:[")
                .Append(type.ContainingAssembly.Identity)
                .Append("]:");

            if (!type.ContainingNamespace.IsGlobalNamespace)
            {
                result.Append(type.ContainingNamespace.ToDisplayString())
                    .Append('.');
            }
        }

        result.Append(type.MetadataName);

        if (type.TypeArguments.IsDefaultOrEmpty)
        {
            return;
        }

        result.Append('<');

        for (var index = 0;
             index < type.TypeArguments.Length;
             index++)
        {
            if (index > 0)
            {
                result.Append(',');
            }

            AppendAlphaEquivalentKey(
                type.TypeArguments[index],
                result,
                typeParameters);
        }

        result.Append('>');
    }

    private static void AppendNamed(
        INamedTypeSymbol type,
        StringBuilder key,
        StringBuilder displayName,
        IReadOnlyDictionary<ITypeParameterSymbol, string>? parameterNames)
    {
        if (type.ContainingType is { } containingType)
        {
            AppendNamed(
                Normalize(containingType),
                key,
                displayName,
                parameterNames);
            key.Append('+');
            displayName.Append('.');
        }
        else
        {
            key.Append("named:[")
                .Append(type.ContainingAssembly.Identity)
                .Append("]:");
            displayName.Append("global::");

            if (!type.ContainingNamespace.IsGlobalNamespace)
            {
                var namespaceName =
                    type.ContainingNamespace.ToDisplayString();

                key.Append(namespaceName).Append('.');
                displayName.Append(namespaceName).Append('.');
            }
        }

        key.Append(type.MetadataName);
        displayName.Append(type.Name);

        if (type.TypeArguments.IsDefaultOrEmpty)
        {
            return;
        }

        key.Append('<');
        displayName.Append('<');

        for (var index = 0;
             index < type.TypeArguments.Length;
             index++)
        {
            if (index > 0)
            {
                key.Append(',');
                displayName.Append(", ");
            }

            Append(
                type.TypeArguments[index],
                key,
                displayName,
                parameterNames);
        }

        key.Append('>');
        displayName.Append('>');
    }

    private static string GetContainingSymbolIdentity(ISymbol symbol)
    {
        if (symbol is INamedTypeSymbol type)
        {
            return type.ContainingAssembly.Identity + ":" +
                   SymbolNameHelper.GetFullMetadataName(type);
        }

        return symbol.GetDocumentationCommentId() ??
               symbol.ToDisplayString(
                   SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static bool AreContainingTypesEquivalent(
        INamedTypeSymbol? left,
        INamedTypeSymbol? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return AreEquivalent(left, right);
    }

    // Incremental analyses may retain source symbols from a previous compilation.
    private static bool SameDefinition(INamedTypeSymbol left, INamedTypeSymbol right) =>
        SymbolEqualityComparer.Default.Equals(left.OriginalDefinition, right.OriginalDefinition) ||
        left.MetadataName == right.MetadataName && left.ContainingAssembly.Identity.Equals(right.ContainingAssembly.Identity) &&
        SymbolNameHelper.GetFullMetadataName(left) == SymbolNameHelper.GetFullMetadataName(right);

    private readonly record struct TypeTerm(ITypeSymbol Type, bool RightScope)
    {
        public TypeTerm Child(ITypeSymbol type) => new(type, RightScope);

        public bool Equals(TypeTerm other) => RightScope == other.RightScope &&
            SymbolEqualityComparer.Default.Equals(Type, other.Type);

        public override int GetHashCode() =>
            unchecked(SymbolEqualityComparer.Default.GetHashCode(Type) * 31 + RightScope.GetHashCode());
    }

    private sealed class Unifier
    {
        private readonly Dictionary<TypeTerm, TypeTerm> _bindings = new();

        public bool Match(TypeTerm left, TypeTerm right)
        {
            left = Resolve(left);
            right = Resolve(right);
            if (left.Type is ITypeParameterSymbol)
                return Bind(left, right);
            if (right.Type is ITypeParameterSymbol)
                return Bind(right, left);

            if (IsObjectLike(left.Type) || IsObjectLike(right.Type))
                return IsObjectLike(left.Type) && IsObjectLike(right.Type);

            if (left.Type is IArrayTypeSymbol la && right.Type is IArrayTypeSymbol ra)
                return la.Rank == ra.Rank && la.IsSZArray == ra.IsSZArray &&
                       Match(left.Child(la.ElementType), right.Child(ra.ElementType));

            if (left.Type is not INamedTypeSymbol ln || right.Type is not INamedTypeSymbol rn)
                return false;

            ln = Normalize(ln);
            rn = Normalize(rn);
            if (!SameDefinition(ln, rn))
                return false;
            if (ln.ContainingType is { } lc && rn.ContainingType is { } rc &&
                !Match(left.Child(lc), right.Child(rc)))
                return false;

            for (var i = 0; i < ln.TypeArguments.Length; i++)
            {
                if (!Match(left.Child(ln.TypeArguments[i]), right.Child(rn.TypeArguments[i])))
                    return false;
            }

            return true;
        }

        public string Format(TypeTerm term, Dictionary<TypeTerm, string> parameters)
        {
            term = Resolve(term);
            if (term.Type is ITypeParameterSymbol)
            {
                if (!parameters.TryGetValue(term, out var name))
                    parameters.Add(term, name = "T" + parameters.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return name;
            }

            return string.Concat(term.Type.ToDisplayParts(SymbolDisplayFormat.FullyQualifiedFormat)
                .Select(part => part.Symbol is ITypeParameterSymbol parameter
                    ? Format(term.Child(parameter), parameters) : part.ToString()));
        }

        private bool Bind(TypeTerm variable, TypeTerm value)
        {
            if (variable.Equals(value))
                return true;
            if (Contains(value, variable))
                return false;

            _bindings.Add(variable, value);
            return true;
        }

        private TypeTerm Resolve(TypeTerm term)
        {
            while (term.Type is ITypeParameterSymbol && _bindings.TryGetValue(term, out var replacement))
                term = replacement;

            return term;
        }

        private bool Contains(TypeTerm term, TypeTerm variable)
        {
            term = Resolve(term);
            if (term.Type is ITypeParameterSymbol)
                return term.Equals(variable);
            if (term.Type is IArrayTypeSymbol array)
                return Contains(term.Child(array.ElementType), variable);

            return term.Type is INamedTypeSymbol named &&
                   (named.ContainingType is { } container && Contains(term.Child(container), variable) ||
                    named.TypeArguments.Any(argument => Contains(term.Child(argument), variable)));
        }
    }

    private static bool IsObjectLike(ITypeSymbol type)
    {
        return type is IDynamicTypeSymbol ||
               type.SpecialType == SpecialType.System_Object;
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
