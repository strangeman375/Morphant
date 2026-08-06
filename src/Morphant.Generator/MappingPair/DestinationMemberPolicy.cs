using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Morphant.Generator.MappingPair;

internal static class DestinationMemberPolicy
{
    public static ImmutableArray<ISymbol> GetSupportedMembers(
        INamedTypeSymbol destinationType,
        Compilation compilation,
        bool includeInitOnlyProperties,
        CancellationToken cancellationToken)
    {
        var planTypeName = GeneratedPlanNaming.BuildMembersTypeName(
            destinationType.OriginalDefinition);
        var result = ImmutableArray.CreateBuilder<ISymbol>();

        if (destinationType.TypeKind == TypeKind.Interface)
        {
            AddInterfaceMembers(
                destinationType,
                planTypeName,
                compilation,
                includeInitOnlyProperties,
                result,
                cancellationToken);
        }
        else
        {
            AddClassMembers(
                destinationType,
                planTypeName,
                compilation,
                includeInitOnlyProperties,
                result,
                cancellationToken);
        }

        return result.ToImmutable();
    }

    private static void AddClassMembers(
        INamedTypeSymbol destinationType,
        string planTypeName,
        Compilation compilation,
        bool includeInitOnlyProperties,
        ImmutableArray<ISymbol>.Builder result,
        CancellationToken cancellationToken)
    {
        var hiddenMemberNames =
            new HashSet<string>(StringComparer.Ordinal);
        var memberGroups = new List<ImmutableArray<ISymbol>>();

        for (var currentType = destinationType;
             currentType is not null;
             currentType = currentType.BaseType)
        {
            cancellationToken.ThrowIfCancellationRequested();

            memberGroups.Add(
                BuildDeclaredMembers(
                    currentType,
                    planTypeName,
                    compilation,
                    includeInitOnlyProperties,
                    hiddenMemberNames,
                    cancellationToken));
        }

        for (var index = memberGroups.Count - 1; index >= 0; index--)
        {
            result.AddRange(memberGroups[index]);
        }
    }

    private static ImmutableArray<ISymbol> BuildDeclaredMembers(
        INamedTypeSymbol declaringType,
        string planTypeName,
        Compilation compilation,
        bool includeInitOnlyProperties,
        HashSet<string> hiddenMemberNames,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<ISymbol>();
        var declaredMembers = declaringType.GetMembers();

        foreach (var member in declaredMembers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!hiddenMemberNames.Contains(member.Name) &&
                !IsGeneratedRecordMemberName(
                    member.Name,
                    planTypeName) &&
                IsSupportedMember(
                    member,
                    compilation,
                    includeInitOnlyProperties))
            {
                result.Add(member);
            }
        }

        foreach (var member in declaredMembers)
        {
            hiddenMemberNames.Add(member.Name);
        }

        return result.ToImmutable();
    }

    private static void AddInterfaceMembers(
        INamedTypeSymbol destinationType,
        string planTypeName,
        Compilation compilation,
        bool includeInitOnlyProperties,
        ImmutableArray<ISymbol>.Builder result,
        CancellationToken cancellationToken)
    {
        var interfaces = BuildBaseFirstInterfaceOrder(
            destinationType,
            cancellationToken);
        var winningDeclarations = BuildWinningInterfaceDeclarations(
            interfaces,
            cancellationToken);
        var emittedMemberNames =
            new HashSet<string>(StringComparer.Ordinal);

        foreach (var currentInterface in interfaces)
        {
            foreach (var member in currentInterface.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!winningDeclarations.TryGetValue(
                        member.Name,
                        out var winningInterface) ||
                    !SymbolEqualityComparer.Default.Equals(
                        currentInterface,
                        winningInterface) ||
                    emittedMemberNames.Contains(member.Name) ||
                    IsGeneratedRecordMemberName(
                        member.Name,
                        planTypeName) ||
                    !IsSupportedMember(
                        member,
                        compilation,
                        includeInitOnlyProperties))
                {
                    continue;
                }

                result.Add(member);
                emittedMemberNames.Add(member.Name);
            }
        }
    }

    private static ImmutableArray<INamedTypeSymbol>
        BuildBaseFirstInterfaceOrder(
            INamedTypeSymbol destinationType,
            CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var visited = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);

        AddInterfaceBaseFirst(
            destinationType,
            visited,
            result,
            cancellationToken);

        return result.ToImmutable();
    }

    private static void AddInterfaceBaseFirst(
        INamedTypeSymbol currentInterface,
        HashSet<ISymbol> visited,
        ImmutableArray<INamedTypeSymbol>.Builder result,
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

    private static Dictionary<string, INamedTypeSymbol>
        BuildWinningInterfaceDeclarations(
            ImmutableArray<INamedTypeSymbol> interfaces,
            CancellationToken cancellationToken)
    {
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

        var result =
            new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);

        foreach (var declaration in declarations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (FindUniqueMostDerivedInterface(
                    declaration.Value,
                    cancellationToken) is { } winningInterface)
            {
                result.Add(declaration.Key, winningInterface);
            }
        }

        return result;
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
        bool includeInitOnlyProperties)
    {
        if (member is IPropertySymbol property)
        {
            if (property.IsStatic ||
                property.IsIndexer ||
                !compilation.IsSymbolAccessibleWithin(
                    property,
                    compilation.Assembly) ||
                !MappingTypeEligibilityPolicy.CanBeNamed(
                    property.Type,
                    compilation))
            {
                return false;
            }

            var canWrite = property.SetMethod is { } setter &&
                (includeInitOnlyProperties || !setter.IsInitOnly) &&
                compilation.IsSymbolAccessibleWithin(
                    setter,
                    compilation.Assembly);
            var isExcludedInitOnly =
                !includeInitOnlyProperties &&
                property.SetMethod?.IsInitOnly == true;
            var canRead = property.GetMethod is { } getter &&
                !isExcludedInitOnly &&
                !property.ReturnsByRef &&
                !property.ReturnsByRefReadonly &&
                compilation.IsSymbolAccessibleWithin(
                    getter,
                    compilation.Assembly);

            return canWrite || canRead;
        }

        return member is IFieldSymbol field &&
               !field.IsStatic &&
               !field.IsConst &&
               !field.IsImplicitlyDeclared &&
               compilation.IsSymbolAccessibleWithin(
                   field,
                   compilation.Assembly) &&
               MappingTypeEligibilityPolicy.CanBeNamed(
                   field.Type,
                   compilation);
    }

    private static bool IsGeneratedRecordMemberName(
        string memberName,
        string planTypeName)
    {
        return memberName == planTypeName ||
               memberName == "Clone" ||
               memberName == "EqualityContract" ||
               memberName == "Equals" ||
               memberName == "GetHashCode" ||
               memberName == "PrintMembers" ||
               memberName == "ToString";
    }
}
