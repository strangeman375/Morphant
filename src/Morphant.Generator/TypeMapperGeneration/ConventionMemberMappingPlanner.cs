using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ConventionMemberMappingPlanner
{
    public static ConventionMemberMappingPlan Build(
        ITypeSymbol sourceType,
        INamedTypeSymbol? destination,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (destination is null)
        {
            return new ConventionMemberMappingPlan(
                [],
                [],
                HasUnmappedRequiredMembers: false);
        }

        var sourceMembers =
            new Dictionary<string, ReadableMember>(
                StringComparer.Ordinal);

        foreach (var memberGroup in BuildEffectiveMemberGroups(
                     sourceType,
                     cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryBuildReadableMember(
                    memberGroup,
                    sourceType,
                    compilation,
                    mapperType) is { } readableMember)
            {
                sourceMembers.Add(
                    readableMember.Name,
                    readableMember);
            }
        }

        var mapNew =
            ImmutableArray.CreateBuilder<
                TypeMapperMemberMappingModel>();
        var mapExisting =
            ImmutableArray.CreateBuilder<
                TypeMapperMemberMappingModel>();
        var candidates =
            ImmutableArray.CreateBuilder<
                MemberTypeCompatibilityCandidate>();
        var candidateRequiredMembers =
            ImmutableArray.CreateBuilder<bool>();
        var hasUnmappedRequiredMembers = false;

        foreach (var memberGroup in BuildEffectiveMemberGroups(
                     destination,
                     cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isRequired = IsRequiredInstanceMember(
                memberGroup);

            if (TryBuildWritableMember(
                    memberGroup,
                    destination,
                    compilation,
                    mapperType) is not { } writableMember ||
                !sourceMembers.TryGetValue(
                    writableMember.Name,
                    out var sourceMember))
            {
                hasUnmappedRequiredMembers |= isRequired;
                continue;
            }

            candidates.Add(
                new MemberTypeCompatibilityCandidate(
                    sourceMember.Name,
                    writableMember.Name,
                    sourceMember.Type,
                    writableMember.Type,
                    writableMember.CanAssign));
            candidateRequiredMembers.Add(isRequired);
        }

        var compatibleCandidates =
            MemberTypeCompatibility.FindCompatibleCandidates(
                sourceType,
                destination,
                candidates.ToImmutable(),
                compilation,
                mapperType,
                cancellationToken);

        for (var index = 0;
             index < candidates.Count;
             index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidate = candidates[index];

            if (!compatibleCandidates[index])
            {
                hasUnmappedRequiredMembers |=
                    candidateRequiredMembers[index];
                continue;
            }

            var mapping = new TypeMapperMemberMappingModel(
                candidate.SourceMemberName,
                candidate.DestinationMemberName);

            mapNew.Add(mapping);

            if (candidate.CanAssign)
            {
                mapExisting.Add(mapping);
            }
        }

        return new ConventionMemberMappingPlan(
            mapNew.ToImmutable(),
            mapExisting.ToImmutable(),
            hasUnmappedRequiredMembers);
    }

    private static ImmutableArray<EffectiveMemberGroup>
        BuildEffectiveMemberGroups(
            ITypeSymbol type,
            CancellationToken cancellationToken)
    {
        if (type is ITypeParameterSymbol typeParameter)
        {
            return BuildTypeParameterMemberGroups(
                typeParameter,
                cancellationToken);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return [];
        }

        return namedType.TypeKind == TypeKind.Interface
            ? BuildInterfaceMemberGroups(
                namedType,
                cancellationToken)
            : BuildClassMemberGroups(
                namedType,
                cancellationToken);
    }

    private static ImmutableArray<EffectiveMemberGroup>
        BuildClassMemberGroups(
            INamedTypeSymbol type,
            CancellationToken cancellationToken)
    {
        var hiddenMemberNames =
            new HashSet<string>(StringComparer.Ordinal);
        var groupsByType =
            new List<ImmutableArray<EffectiveMemberGroup>>();

        for (var current = type;
             current is not null;
             current = current.BaseType)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var declaredMembers = current.GetMembers();
            var declaredNames =
                new HashSet<string>(StringComparer.Ordinal);
            var groups =
                ImmutableArray.CreateBuilder<
                    EffectiveMemberGroup>();

            foreach (var member in declaredMembers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!declaredNames.Add(member.Name) ||
                    hiddenMemberNames.Contains(member.Name))
                {
                    continue;
                }

                groups.Add(
                    new EffectiveMemberGroup(
                        member.Name,
                        current.GetMembers(member.Name)));
            }

            foreach (var member in declaredMembers)
            {
                hiddenMemberNames.Add(member.Name);
            }

            groupsByType.Add(groups.ToImmutable());
        }

        var result =
            ImmutableArray.CreateBuilder<EffectiveMemberGroup>();

        for (var index = groupsByType.Count - 1;
             index >= 0;
             index--)
        {
            result.AddRange(groupsByType[index]);
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<EffectiveMemberGroup>
        BuildInterfaceMemberGroups(
            INamedTypeSymbol type,
            CancellationToken cancellationToken)
    {
        var interfaces = BuildBaseFirstInterfaceOrder(
            type,
            cancellationToken);

        return BuildInterfaceMemberGroups(
            interfaces,
            cancellationToken);
    }

    private static ImmutableArray<EffectiveMemberGroup>
        BuildInterfaceMemberGroups(
            ImmutableArray<INamedTypeSymbol> interfaces,
            CancellationToken cancellationToken)
    {
        var winningDeclarations =
            BuildWinningInterfaceDeclarations(
                interfaces,
                cancellationToken);
        var emittedNames =
            new HashSet<string>(StringComparer.Ordinal);
        var result =
            ImmutableArray.CreateBuilder<EffectiveMemberGroup>();

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
                    !emittedNames.Add(member.Name))
                {
                    continue;
                }

                result.Add(
                    new EffectiveMemberGroup(
                        member.Name,
                        currentInterface.GetMembers(member.Name)));
            }
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<EffectiveMemberGroup>
        BuildTypeParameterMemberGroups(
            ITypeParameterSymbol typeParameter,
            CancellationToken cancellationToken)
    {
        var result =
            ImmutableArray.CreateBuilder<EffectiveMemberGroup>();
        var emittedNames =
            new HashSet<string>(StringComparer.Ordinal);

        var constraintTypes = BuildConstraintTypes(
            typeParameter,
            cancellationToken);

        var classConstraint = constraintTypes
            .FirstOrDefault(
                static constraint =>
                    constraint.TypeKind == TypeKind.Class);

        if (classConstraint is not null)
        {
            foreach (var memberGroup in BuildClassMemberGroups(
                         classConstraint,
                         cancellationToken))
            {
                if (emittedNames.Add(memberGroup.Name))
                {
                    result.Add(memberGroup);
                }
            }
        }

        var interfaceRoots = constraintTypes
            .Where(
                static constraint =>
                    constraint.TypeKind ==
                    TypeKind.Interface)
            .ToImmutableArray();

        if (!interfaceRoots.IsEmpty)
        {
            var interfaces = BuildBaseFirstInterfaceOrder(
                interfaceRoots,
                cancellationToken);

            foreach (var memberGroup in BuildInterfaceMemberGroups(
                         interfaces,
                         cancellationToken))
            {
                if (emittedNames.Add(memberGroup.Name))
                {
                    result.Add(memberGroup);
                }
            }
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<INamedTypeSymbol>
        BuildConstraintTypes(
            ITypeParameterSymbol typeParameter,
            CancellationToken cancellationToken)
    {
        var result =
            ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var visitedTypeParameters =
            new HashSet<ISymbol>(
                SymbolEqualityComparer.Default);
        var visitedTypes =
            new HashSet<ISymbol>(
                SymbolEqualityComparer.Default);

        AddConstraintTypes(
            typeParameter,
            visitedTypeParameters,
            visitedTypes,
            result,
            cancellationToken);

        return result.ToImmutable();
    }

    private static void AddConstraintTypes(
        ITypeParameterSymbol typeParameter,
        HashSet<ISymbol> visitedTypeParameters,
        HashSet<ISymbol> visitedTypes,
        ImmutableArray<INamedTypeSymbol>.Builder result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!visitedTypeParameters.Add(typeParameter))
        {
            return;
        }

        foreach (var constraint in typeParameter.ConstraintTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (constraint is ITypeParameterSymbol nestedTypeParameter)
            {
                AddConstraintTypes(
                    nestedTypeParameter,
                    visitedTypeParameters,
                    visitedTypes,
                    result,
                    cancellationToken);
            }
            else if (constraint is INamedTypeSymbol namedType &&
                     visitedTypes.Add(namedType))
            {
                result.Add(namedType);
            }
        }
    }

    private static ImmutableArray<INamedTypeSymbol>
        BuildBaseFirstInterfaceOrder(
            INamedTypeSymbol type,
            CancellationToken cancellationToken)
    {
        return BuildBaseFirstInterfaceOrder(
            ImmutableArray.Create(type),
            cancellationToken);
    }

    private static ImmutableArray<INamedTypeSymbol>
        BuildBaseFirstInterfaceOrder(
            ImmutableArray<INamedTypeSymbol> types,
            CancellationToken cancellationToken)
    {
        var result =
            ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var visited =
            new HashSet<ISymbol>(
                SymbolEqualityComparer.Default);

        foreach (var type in types)
        {
            AddInterfaceBaseFirst(
                type,
                visited,
                result,
                cancellationToken);
        }

        return result.ToImmutable();
    }

    private static void AddInterfaceBaseFirst(
        INamedTypeSymbol type,
        HashSet<ISymbol> visited,
        ImmutableArray<INamedTypeSymbol>.Builder result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!visited.Add(type))
        {
            return;
        }

        foreach (var baseInterface in type.Interfaces)
        {
            AddInterfaceBaseFirst(
                baseInterface,
                visited,
                result,
                cancellationToken);
        }

        result.Add(type);
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
                    declaringInterfaces =
                        new List<INamedTypeSymbol>();
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
            new Dictionary<string, INamedTypeSymbol>(
                StringComparer.Ordinal);

        foreach (var declaration in declarations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (FindUniqueMostDerivedInterface(
                    declaration.Value,
                    cancellationToken) is { } winningInterface)
            {
                result.Add(
                    declaration.Key,
                    winningInterface);
            }
        }

        return result;
    }

    private static INamedTypeSymbol?
        FindUniqueMostDerivedInterface(
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
                        InheritsFromInterface(
                            other,
                            candidate)))
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

    private static bool InheritsFromInterface(
        INamedTypeSymbol derived,
        INamedTypeSymbol baseInterface)
    {
        return derived.AllInterfaces.Any(
            candidate =>
                SymbolEqualityComparer.Default.Equals(
                    candidate,
                    baseInterface));
    }

    private static ReadableMember? TryBuildReadableMember(
        EffectiveMemberGroup memberGroup,
        ITypeSymbol source,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType)
    {
        foreach (var member in memberGroup.Members)
        {
            if (member is IPropertySymbol property)
            {
                if (property.IsStatic ||
                    property.IsIndexer ||
                    property.ReturnsByRef ||
                    property.ReturnsByRefReadonly ||
                    !property.ExplicitInterfaceImplementations.IsEmpty ||
                    property.GetMethod is not { } getter ||
                    !IsAccessible(
                        property,
                        source,
                        compilation,
                        mapperType) ||
                    !IsAccessible(
                        getter,
                        source,
                        compilation,
                        mapperType))
                {
                    continue;
                }

                return new ReadableMember(
                    property.Name,
                    property.Type);
            }

            if (member is IFieldSymbol field &&
                !field.IsStatic &&
                !field.IsConst &&
                !field.IsImplicitlyDeclared &&
                !field.IsFixedSizeBuffer &&
                IsAccessible(
                    field,
                    source,
                    compilation,
                    mapperType))
            {
                return new ReadableMember(
                    field.Name,
                    field.Type);
            }
        }

        return null;
    }

    private static WritableMember? TryBuildWritableMember(
        EffectiveMemberGroup memberGroup,
        INamedTypeSymbol destination,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType)
    {
        foreach (var member in memberGroup.Members)
        {
            if (member is IPropertySymbol property)
            {
                if (property.IsStatic ||
                    property.IsIndexer ||
                    property.ReturnsByRef ||
                    property.ReturnsByRefReadonly ||
                    !property.ExplicitInterfaceImplementations.IsEmpty ||
                    property.SetMethod is not { } setter ||
                    !IsAccessible(
                        property,
                        destination,
                        compilation,
                        mapperType) ||
                    !IsAccessible(
                        setter,
                        destination,
                        compilation,
                        mapperType))
                {
                    continue;
                }

                return new WritableMember(
                    property.Name,
                    property.Type,
                    CanAssign: !setter.IsInitOnly);
            }

            if (member is IFieldSymbol field &&
                !field.IsStatic &&
                !field.IsConst &&
                !field.IsReadOnly &&
                !field.IsImplicitlyDeclared &&
                !field.IsFixedSizeBuffer &&
                IsAccessible(
                    field,
                    destination,
                    compilation,
                    mapperType))
            {
                return new WritableMember(
                    field.Name,
                    field.Type,
                    CanAssign: true);
            }
        }

        return null;
    }

    private static bool IsAccessible(
        ISymbol symbol,
        ITypeSymbol throughType,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType)
    {
        return compilation.IsSymbolAccessibleWithin(
            symbol,
            mapperType,
            throughType);
    }

    private static bool IsRequiredInstanceMember(
        EffectiveMemberGroup memberGroup)
    {
        return memberGroup.Members.Any(
            static member =>
                !member.IsStatic &&
                member is IPropertySymbol
                {
                    IsRequired: true
                } or IFieldSymbol
                {
                    IsRequired: true
                });
    }

    private readonly record struct EffectiveMemberGroup(
        string Name,
        ImmutableArray<ISymbol> Members);

    private readonly record struct ReadableMember(
        string Name,
        ITypeSymbol Type);

    private readonly record struct WritableMember(
        string Name,
        ITypeSymbol Type,
        bool CanAssign);
}

internal readonly record struct ConventionMemberMappingPlan(
    ImmutableArray<TypeMapperMemberMappingModel> MapNew,
    ImmutableArray<TypeMapperMemberMappingModel> MapExisting,
    bool HasUnmappedRequiredMembers);
