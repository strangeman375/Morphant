using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class DestinationCapabilityPolicy
{
    public static MappingPairCapabilities Build(
        ITypeSymbol destinationType,
        Compilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var destination = GetDestinationType(
            destinationType,
            compilation);

        var isOpaque = IsOpaque(destination);
        var hasStructuredConstruction =
            !isOpaque &&
            HasSupportedParameterizedConstructor(
                destination,
                compilation,
                mapperType,
                cancellationToken);
        var hasParameterlessConstruction =
            !isOpaque &&
            HasParameterlessConstruction(
                destination,
                compilation,
                mapperType,
                cancellationToken);
        var hasMembers =
            !isOpaque &&
            HasSupportedMember(
                destination,
                compilation,
                mapperType,
                cancellationToken);

        return new MappingPairCapabilities(
            Runtime: true,
            Manual: true,
            hasStructuredConstruction
                ? MappingConstructionKind.Structured
                : MappingConstructionKind.Direct,
            hasParameterlessConstruction,
            hasMembers);
    }

    private static INamedTypeSymbol GetDestinationType(
        ITypeSymbol destinationType,
        Compilation compilation)
    {
        if (destinationType is IDynamicTypeSymbol)
        {
            return compilation.GetSpecialType(
                SpecialType.System_Object);
        }

        var namedType = (INamedTypeSymbol)destinationType;

        return namedType.OriginalDefinition.SpecialType ==
                   SpecialType.System_Nullable_T
            ? (INamedTypeSymbol)namedType.TypeArguments[0]
            : namedType;
    }

    private static bool IsOpaque(INamedTypeSymbol destinationType)
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

    private static bool HasSupportedParameterizedConstructor(
        INamedTypeSymbol destinationType,
        Compilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (destinationType.TypeKind == TypeKind.Interface ||
            destinationType.IsAbstract)
        {
            return false;
        }

        foreach (var constructor in destinationType.InstanceConstructors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (constructor.Parameters.Length == 0 ||
                !compilation.IsSymbolAccessibleWithin(
                    constructor,
                    mapperType) ||
                constructor.Parameters.Any(
                    parameter =>
                        parameter.RefKind != RefKind.None ||
                        parameter.Type.IsRefLikeType ||
                        !MappingTypeEligibilityPolicy.CanBeNamed(
                            parameter.Type,
                            compilation,
                            mapperType)))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool HasParameterlessConstruction(
        INamedTypeSymbol destinationType,
        Compilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (destinationType.TypeKind == TypeKind.Interface ||
            destinationType.IsAbstract)
        {
            return false;
        }

        if (destinationType.IsValueType)
        {
            return true;
        }

        foreach (var constructor in destinationType.InstanceConstructors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (constructor.Parameters.Length == 0 &&
                compilation.IsSymbolAccessibleWithin(
                    constructor,
                    mapperType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSupportedMember(
        INamedTypeSymbol destinationType,
        Compilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        return destinationType.TypeKind == TypeKind.Interface
            ? HasSupportedInterfaceMember(
                destinationType,
                compilation,
                mapperType,
                cancellationToken)
            : HasSupportedClassMember(
                destinationType,
                compilation,
                mapperType,
                cancellationToken);
    }

    private static bool HasSupportedClassMember(
        INamedTypeSymbol destinationType,
        Compilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var hiddenMemberNames =
            new HashSet<string>(StringComparer.Ordinal);

        for (var currentType = destinationType;
             currentType is not null;
             currentType = currentType.BaseType)
        {
            var declaredMembers = currentType.GetMembers();

            foreach (var member in declaredMembers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!hiddenMemberNames.Contains(member.Name) &&
                    IsSupportedMember(
                        member,
                        compilation,
                        mapperType))
                {
                    return true;
                }
            }

            foreach (var member in declaredMembers)
            {
                hiddenMemberNames.Add(member.Name);
            }
        }

        return false;
    }

    private static bool HasSupportedInterfaceMember(
        INamedTypeSymbol destinationType,
        Compilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var interfaces = BuildBaseFirstInterfaceOrder(
            destinationType,
            cancellationToken);
        var declarations =
            new Dictionary<string, List<INamedTypeSymbol>>(
                StringComparer.Ordinal);

        foreach (var currentInterface in interfaces)
        {
            foreach (var member in currentInterface.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!declarations.TryGetValue(
                        member.Name,
                        out var declaringInterfaces))
                {
                    declaringInterfaces = new List<INamedTypeSymbol>();
                    declarations.Add(
                        member.Name,
                        declaringInterfaces);
                }

                if (!declaringInterfaces.Any(
                        candidate =>
                            SymbolEqualityComparer.Default.Equals(
                                candidate,
                                currentInterface)))
                {
                    declaringInterfaces.Add(currentInterface);
                }
            }
        }

        foreach (var declaration in declarations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (FindUniqueMostDerivedInterface(
                    declaration.Value,
                    cancellationToken) is not
                    { } winningInterface)
            {
                continue;
            }

            foreach (var member in winningInterface.GetMembers(
                         declaration.Key))
            {
                if (IsSupportedMember(
                        member,
                        compilation,
                        mapperType))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static List<INamedTypeSymbol> BuildBaseFirstInterfaceOrder(
        INamedTypeSymbol destinationType,
        CancellationToken cancellationToken)
    {
        var result = new List<INamedTypeSymbol>();
        var visited = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);

        AddInterfaceBaseFirst(
            destinationType,
            visited,
            result,
            cancellationToken);

        return result;
    }

    private static void AddInterfaceBaseFirst(
        INamedTypeSymbol currentInterface,
        HashSet<ISymbol> visited,
        List<INamedTypeSymbol> result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!visited.Add(currentInterface))
        {
            return;
        }

        foreach (var baseInterface in currentInterface.Interfaces)
        {
            AddInterfaceBaseFirst(
                baseInterface,
                visited,
                result,
                cancellationToken);
        }

        result.Add(currentInterface);
    }

    private static INamedTypeSymbol? FindUniqueMostDerivedInterface(
        List<INamedTypeSymbol> declaringInterfaces,
        CancellationToken cancellationToken)
    {
        INamedTypeSymbol? result = null;

        foreach (var candidate in declaringInterfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (declaringInterfaces.Any(
                    other =>
                        !SymbolEqualityComparer.Default.Equals(
                            candidate,
                            other) &&
                        other.AllInterfaces.Any(
                            inherited =>
                                SymbolEqualityComparer.Default.Equals(
                                    inherited,
                                    candidate))))
            {
                continue;
            }

            if (result is not null)
            {
                return null;
            }

            result = candidate;
        }

        return result;
    }

    private static bool IsSupportedMember(
        ISymbol member,
        Compilation compilation,
        INamedTypeSymbol mapperType)
    {
        if (member is IPropertySymbol property)
        {
            return !property.IsStatic &&
                   !property.IsIndexer &&
                   property.SetMethod is { } setter &&
                   compilation.IsSymbolAccessibleWithin(
                       property,
                       mapperType) &&
                   compilation.IsSymbolAccessibleWithin(
                       setter,
                       mapperType) &&
                   MappingTypeEligibilityPolicy.CanBeNamed(
                       property.Type,
                       compilation,
                       mapperType);
        }

        return member is IFieldSymbol field &&
               !field.IsStatic &&
               !field.IsConst &&
               !field.IsReadOnly &&
               !field.IsImplicitlyDeclared &&
               compilation.IsSymbolAccessibleWithin(
                   field,
                   mapperType) &&
               MappingTypeEligibilityPolicy.CanBeNamed(
                   field.Type,
                   compilation,
                   mapperType);
    }
}
