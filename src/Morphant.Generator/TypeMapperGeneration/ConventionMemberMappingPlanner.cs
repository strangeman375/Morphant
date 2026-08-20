using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.MappingPair;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class ConventionMemberMappingPlanner
{
    private const string AllowNullAttributeMetadataName =
        "System.Diagnostics.CodeAnalysis.AllowNullAttribute";

    private const string DisallowNullAttributeMetadataName =
        "System.Diagnostics.CodeAnalysis.DisallowNullAttribute";

    public static ConventionMemberMappingPlan Build(
        ITypeSymbol sourceType,
        ITypeSymbol? destination,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        return Build(
            sourceType,
            destination,
            compilation,
            mapperType,
            mapperType,
            includeInitOnlyProperties: true,
            hasMemberCapability: true,
            excludeGeneratedPlanMemberNames: false,
            sourceContext: null,
            sourceValueName: "source",
            cancellationToken);
    }

    public static ConventionMemberMappingPlan Build(
        ITypeSymbol sourceType,
        ITypeSymbol? destination,
        MappingPairCapabilities capabilities,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        return Build(
            sourceType,
            destination,
            compilation,
            mapperType,
            compilation.Assembly,
            capabilities.StructuredConstruction,
            capabilities.Members,
            excludeGeneratedPlanMemberNames: true,
            sourceContext: null,
            sourceValueName: "source",
            cancellationToken);
    }

    public static ConventionMemberMappingPlan Build(
        ITypeSymbol sourceType,
        ITypeSymbol? destination,
        MappingPairCapabilities capabilities,
        ConventionSourceMemberContext sourceContext,
        string sourceValueName,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        return Build(
            sourceType,
            destination,
            compilation,
            mapperType,
            compilation.Assembly,
            capabilities.StructuredConstruction,
            capabilities.Members,
            excludeGeneratedPlanMemberNames: true,
            sourceContext,
            sourceValueName,
            cancellationToken);
    }

    private static ConventionMemberMappingPlan Build(
        ITypeSymbol sourceType,
        ITypeSymbol? destination,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        ISymbol destinationAccessWithin,
        bool includeInitOnlyProperties,
        bool hasMemberCapability,
        bool excludeGeneratedPlanMemberNames,
        ConventionSourceMemberContext? sourceContext,
        string sourceValueName,
        CancellationToken cancellationToken)
    {
        if (destination is null)
        {
            return new ConventionMemberMappingPlan(
                ImmutableArray<TypeMapperMemberMappingModel>.Empty,
                ImmutableArray<TypeMapperMemberMappingModel>.Empty,
                ImmutableArray<TypeMapperMemberMappingModel>.Empty,
                ImmutableArray<TypeMapperMemberMappingModel>.Empty,
                ImmutableArray<TypeMapperMemberMappingModel>.Empty,
                new MemberPlanningObservation(
                    ImmutableArray<ISymbol>.Empty,
                    ImmutableArray<ISymbol>.Empty,
                    ImmutableArray<MemberRuleObservation>.Empty,
                    ImmutableArray<ISymbol>.Empty,
                    ImmutableArray<StructuredTerminalObservation>.Empty));
        }

        var readableMembers = sourceContext?.DirectMembers ?? default;

        if (readableMembers.IsDefault)
        {
            readableMembers = BuildReadableMembers(
                sourceType,
                compilation,
                mapperType,
                cancellationToken);
        }
        var resolvedSourceContext = sourceContext ??
            new ConventionSourceMemberContext(
                sourceType,
                readableMembers,
                ImmutableArray<IncludedSourceScope>.Empty,
                FlatteningValue.None);

        var create =
            ImmutableArray.CreateBuilder<
                TypeMapperMemberMappingModel>();
        var update =
            ImmutableArray.CreateBuilder<
                TypeMapperMemberMappingModel>();
        var candidates =
            ImmutableArray.CreateBuilder<
                MemberTypeCompatibilityCandidate>();
        var candidateMembers =
            ImmutableArray.CreateBuilder<ConventionReadableMember>();
        var candidateGroups =
            ImmutableArray.CreateBuilder<MemberCandidateGroup>();
        var candidateValueExpressions =
            ImmutableArray.CreateBuilder<
                ConventionSourceValueExpressionModel?>();
        var supportedDestinationMembers =
            ImmutableArray.CreateBuilder<ISymbol>();
        var requiredObligations =
            ImmutableArray.CreateBuilder<ISymbol>();
        var rules =
            ImmutableArray.CreateBuilder<MemberRuleObservation>();
        var flatteningIssues =
            ImmutableArray.CreateBuilder<FlatteningIssueObservation>();

        foreach (var memberGroup in BuildEffectiveMemberGroups(
                     destination,
                     cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var isRequired = IsRequiredInstanceMember(
                memberGroup);
            var requiredMember = isRequired
                ? FindRequiredMember(memberGroup)
                : null;

            var writableMember = hasMemberCapability &&
                !(excludeGeneratedPlanMemberNames &&
                  IsGeneratedPlanMemberName(
                      memberGroup.Name,
                      destination))
                    ? TryBuildWritableMember(
                        memberGroup,
                        destination,
                        compilation,
                        destinationAccessWithin,
                        includeInitOnlyProperties)
                    : null;

            if (writableMember is { } supportedWritable)
            {
                supportedDestinationMembers.Add(
                    supportedWritable.Symbol);
            }

            if (writableMember is not { } selectedWritable)
            {
                if (requiredMember is not null)
                {
                    requiredObligations.Add(requiredMember);
                }

                continue;
            }

            var resolution = ConventionSourceMemberResolver.ResolveExact(
                resolvedSourceContext,
                selectedWritable.Name,
                compilation,
                mapperType,
                cancellationToken);

            if (resolution.Candidates.IsEmpty)
            {
                if (requiredMember is not null)
                {
                    requiredObligations.Add(requiredMember);
                }

                continue;
            }

            var firstCandidate = candidates.Count;

            foreach (var sourceMember in resolution.Candidates)
            {
                var candidateIndex = candidates.Count;
                candidates.Add(
                    new MemberTypeCompatibilityCandidate(
                        sourceMember.Name,
                        selectedWritable.Name,
                        sourceMember.Type,
                        selectedWritable.Type,
                        selectedWritable.CanAssign,
                        sourceMember.BuildConventionValueExpression(
                            "source!")));
                candidateMembers.Add(sourceMember);
                candidateValueExpressions.Add(
                    sourceMember.BuildConventionValueExpression(
                        sourceValueName));
            }

            var firstFallbackCandidate = candidates.Count;

            foreach (var sourceMember in resolution.FallbackCandidates)
            {
                var candidateIndex = candidates.Count;
                candidates.Add(
                    new MemberTypeCompatibilityCandidate(
                        sourceMember.Name,
                        selectedWritable.Name,
                        sourceMember.Type,
                        selectedWritable.Type,
                        selectedWritable.CanAssign,
                        sourceMember.BuildConventionValueExpression(
                            "source!")));
                candidateMembers.Add(sourceMember);
                candidateValueExpressions.Add(
                    sourceMember.BuildConventionValueExpression(
                        sourceValueName));
            }

            candidateGroups.Add(new MemberCandidateGroup(
                selectedWritable,
                isRequired,
                requiredMember,
                resolution.HasDirectClaim,
                firstCandidate,
                resolution.Candidates.Length,
                firstFallbackCandidate,
                resolution.FallbackCandidates.Length));
        }

        var compatibleCandidates =
            MemberTypeCompatibility.FindCompatibleCandidates(
                sourceType,
                destination,
                candidates.ToImmutable(),
                compilation,
                mapperType,
                cancellationToken);

        foreach (var group in candidateGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var compatible = Enumerable.Range(
                    group.FirstCandidate,
                    group.CandidateCount)
                .Where(index => compatibleCandidates[index])
                .ToImmutableArray();

            if (!group.HasDirectClaim &&
                compatible.IsEmpty &&
                group.FallbackCandidateCount > 0)
            {
                compatible = Enumerable.Range(
                        group.FirstFallbackCandidate,
                        group.FallbackCandidateCount)
                    .Where(index => compatibleCandidates[index])
                    .ToImmutableArray();
            }

            if (compatible.Length == 0 ||
                group.HasDirectClaim && compatible.Length != 1)
            {
                if (group.RequiredMember is { } requiredMember)
                {
                    requiredObligations.Add(requiredMember);
                }

                continue;
            }

            if (compatible.Length > 1)
            {
                var ambiguousMembers = compatible
                    .Select(index => candidateMembers[index])
                    .ToImmutableArray();
                flatteningIssues.Add(BuildFlatteningIssue(
                    group.Destination.Symbol,
                    group.Destination.Name,
                    ambiguousMembers));

                if (group.RequiredMember is { } requiredMember)
                {
                    requiredObligations.Add(requiredMember);
                }

                continue;
            }

            var selectedIndex = compatible[0];
            var candidate = candidates[selectedIndex];
            var sourceMember = candidateMembers[selectedIndex];

            var mapping = new TypeMapperMemberMappingModel(
                candidate.SourceMemberName,
                candidate.DestinationMemberName,
                group.IsRequired,
                SourceValueLocalName: null,
                ConventionValueExpression:
                    candidateValueExpressions[selectedIndex]);

            create.Add(mapping);

            rules.Add(
                new MemberRuleObservation(
                    group.Destination.Symbol,
                    sourceMember.Symbol,
                    MemberRuleOrigin.Convention,
                    OriginNode: null,
                    group.IsRequired,
                    MemberLifecycleDependency.Creation |
                    (candidate.CanAssign
                        ? MemberLifecycleDependency.ExistingDestination
                        : MemberLifecycleDependency.InitOnly),
                    HiddenImportedSlot: null,
                    SourcePathMembers:
                        sourceMember.GetSourcePathMembers()));

            if (candidate.CanAssign)
            {
                update.Add(mapping);
            }
        }

        var immutableUpdate = update.ToImmutable();

        var immutableCreate = create.ToImmutable();

        return new ConventionMemberMappingPlan(
            immutableCreate,
            immutableUpdate,
            immutableCreate,
            immutableUpdate,
            immutableUpdate,
            new MemberPlanningObservation(
                readableMembers.Select(static member => member.Symbol)
                    .ToImmutableArray(),
                supportedDestinationMembers.ToImmutable(),
                rules.ToImmutable(),
                requiredObligations.ToImmutable(),
                Terminals: ImmutableArray<StructuredTerminalObservation>.Empty,
                FlatteningIssues: flatteningIssues.ToImmutable()));
    }

    internal static ImmutableArray<ConventionReadableMember>
        BuildReadableMembers(
            ITypeSymbol sourceType,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            CancellationToken cancellationToken)
    {
        var result =
            ImmutableArray.CreateBuilder<
                ConventionReadableMember>();

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
                result.Add(readableMember);
            }
        }

        return result.ToImmutable();
    }

    internal static ImmutableArray<ConventionWritableMember>
        BuildWritableMembers(
            ITypeSymbol destination,
            MappingPairCapabilities capabilities,
            CSharpCompilation compilation,
            CancellationToken cancellationToken)
    {
        if (!capabilities.Members)
        {
            return ImmutableArray<ConventionWritableMember>.Empty;
        }

        var result =
            ImmutableArray.CreateBuilder<ConventionWritableMember>();

        foreach (var memberGroup in BuildEffectiveMemberGroups(
                     destination,
                     cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsGeneratedPlanMemberName(
                    memberGroup.Name,
                    destination) ||
                TryBuildWritableMember(
                    memberGroup,
                    destination,
                    compilation,
                    compilation.Assembly,
                    capabilities.StructuredConstruction) is not
                    { } writableMember)
            {
                continue;
            }

            result.Add(
                new ConventionWritableMember(
                    writableMember.Name,
                    writableMember.Type,
                    writableMember.CanAssign,
                    IsRequiredInstanceMember(memberGroup),
                    writableMember.Symbol));
        }

        return result.ToImmutable();
    }

    internal static ISymbol? FindEffectiveInstanceMember(
        ITypeSymbol type,
        string memberName,
        CancellationToken cancellationToken)
    {
        var group = BuildEffectiveMemberGroups(type, cancellationToken)
            .FirstOrDefault(candidate => StringComparer.Ordinal.Equals(
                candidate.Name,
                memberName));

        if (group.Members.IsDefaultOrEmpty)
        {
            return null;
        }

        return group.Members.FirstOrDefault(static member =>
            !member.IsStatic &&
            member is IPropertySymbol or IFieldSymbol) ??
               group.Members.FirstOrDefault(static member =>
                   !member.IsStatic);
    }

    internal static ImmutableArray<ISymbol> FindUnmappedRequiredMembers(
        ITypeSymbol destination,
        ImmutableArray<TypeMapperMemberMappingModel> mappings,
        CancellationToken cancellationToken)
    {
        var result = ImmutableArray.CreateBuilder<ISymbol>();
        var mappedNames = new HashSet<string>(
            mappings.Select(
                static mapping => mapping.DestinationMemberName),
            StringComparer.Ordinal);

        foreach (var memberGroup in BuildEffectiveMemberGroups(
                     destination,
                     cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsRequiredInstanceMember(memberGroup) &&
                !mappedNames.Contains(memberGroup.Name))
            {
                result.Add(FindRequiredMember(memberGroup)!);
            }
        }

        return result.ToImmutable();
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
            return ImmutableArray<EffectiveMemberGroup>.Empty;
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

    private static ConventionReadableMember?
        TryBuildReadableMember(
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

                return new ConventionReadableMember(
                    property.Name,
                    property.Type,
                    property);
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
                return new ConventionReadableMember(
                    field.Name,
                    field.Type,
                    field);
            }
        }

        return null;
    }

    private static WritableMember? TryBuildWritableMember(
        EffectiveMemberGroup memberGroup,
        ITypeSymbol destination,
        CSharpCompilation compilation,
        ISymbol destinationAccessWithin,
        bool includeInitOnlyProperties)
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
                    !includeInitOnlyProperties && setter.IsInitOnly ||
                    !IsAccessible(
                        property,
                        destination,
                        compilation,
                        destinationAccessWithin) ||
                    !IsAccessible(
                        setter,
                        destination,
                        compilation,
                        destinationAccessWithin))
                {
                    continue;
                }

                return new WritableMember(
                    property.Name,
                    GetInputType(
                        property.Type,
                        setter.Parameters[setter.Parameters.Length - 1]
                            .NullableAnnotation,
                        property,
                        setter.Parameters[setter.Parameters.Length - 1]),
                    CanAssign: !setter.IsInitOnly,
                    property);
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
                    destinationAccessWithin))
            {
                return new WritableMember(
                    field.Name,
                    GetInputType(
                        field.Type,
                        field.NullableAnnotation,
                        field,
                        inputSymbol: null),
                    CanAssign: true,
                    field);
            }
        }

        return null;
    }

    private static ITypeSymbol GetInputType(
        ITypeSymbol type,
        NullableAnnotation nullableAnnotation,
        ISymbol member,
        ISymbol? inputSymbol)
    {
        if (type.IsReferenceType ||
            type.TypeKind == TypeKind.TypeParameter)
        {
            if (HasAttribute(
                    member,
                    DisallowNullAttributeMetadataName) ||
                HasAttribute(
                    inputSymbol,
                    DisallowNullAttributeMetadataName))
            {
                nullableAnnotation = NullableAnnotation.NotAnnotated;
            }
            else if (HasAttribute(
                         member,
                         AllowNullAttributeMetadataName) ||
                     HasAttribute(
                         inputSymbol,
                         AllowNullAttributeMetadataName))
            {
                nullableAnnotation = NullableAnnotation.Annotated;
            }
        }

        return type.WithNullableAnnotation(nullableAnnotation);
    }

    private static bool HasAttribute(
        ISymbol? symbol,
        string metadataName)
    {
        return symbol?.GetAttributes().Any(attribute =>
                   attribute.AttributeClass is { } attributeType &&
                   StringComparer.Ordinal.Equals(
                       SymbolNameHelper.GetFullMetadataName(attributeType),
                       metadataName)) == true;
    }

    private static bool IsAccessible(
        ISymbol symbol,
        ITypeSymbol throughType,
        CSharpCompilation compilation,
        ISymbol within)
    {
        return compilation.IsSymbolAccessibleWithin(
            symbol,
            within,
            throughType);
    }

    private static bool IsGeneratedPlanMemberName(
        string memberName,
        ITypeSymbol destination)
    {
        if (destination is not INamedTypeSymbol namedDestination)
        {
            return false;
        }

        var planTypeName = GeneratedPlanNaming.BuildMembersTypeName(
            namedDestination.OriginalDefinition);

        return memberName == planTypeName ||
               memberName == "Clone" ||
               memberName == "EqualityContract" ||
               memberName == "Equals" ||
               memberName == "GetHashCode" ||
               memberName == "PrintMembers" ||
               memberName == "ToString";
    }

    private static ISymbol? FindRequiredMember(
        EffectiveMemberGroup memberGroup)
    {
        return memberGroup.Members.FirstOrDefault(
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

    private static bool IsRequiredInstanceMember(
        EffectiveMemberGroup memberGroup)
    {
        return FindRequiredMember(memberGroup) is not null;
    }

    internal static FlatteningIssueObservation BuildFlatteningIssue(
        ISymbol targetSymbol,
        string targetName,
        ImmutableArray<ConventionReadableMember> candidates,
        SyntaxNode? originNode = null)
    {
        var locations = candidates
            .SelectMany(static candidate =>
                candidate.GetSourcePathMembers())
            .SelectMany(static member => member.Locations)
            .Where(static location => location.IsInSource)
            .GroupBy(static location =>
                (location.SourceTree?.FilePath ?? string.Empty) + "|" +
                location.SourceSpan.Start + "|" +
                location.SourceSpan.Length,
                StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToImmutableArray();

        return new FlatteningIssueObservation(
            targetName,
            targetSymbol,
            originNode,
            candidates.Select(static candidate =>
                    candidate.GetPathDisplay())
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToImmutableArray(),
            locations);
    }

    private readonly record struct EffectiveMemberGroup(
        string Name,
        ImmutableArray<ISymbol> Members);

    private readonly record struct WritableMember(
        string Name,
        ITypeSymbol Type,
        bool CanAssign,
        ISymbol Symbol);

    private readonly record struct MemberCandidateGroup(
        WritableMember Destination,
        bool IsRequired,
        ISymbol? RequiredMember,
        bool HasDirectClaim,
        int FirstCandidate,
        int CandidateCount,
        int FirstFallbackCandidate,
        int FallbackCandidateCount);
}

internal readonly record struct ConventionMemberMappingPlan(
    ImmutableArray<TypeMapperMemberMappingModel> Create,
    ImmutableArray<TypeMapperMemberMappingModel> CreatePost,
    ImmutableArray<TypeMapperMemberMappingModel> MapReplacement,
    ImmutableArray<TypeMapperMemberMappingModel> MapReplacementPost,
    ImmutableArray<TypeMapperMemberMappingModel> Update,
    MemberPlanningObservation Observation,
    ImmutableArray<string> ConfiguredMemberNames = default,
    MappingFailureObservation? Failure = null)
{
    public ConstructorInitializationMappingPlan BuildConstructorInitializationPlan(
        bool replacement)
    {
        var initializerMappings = replacement
            ? MapReplacement
            : Create;
        var postMappings = (replacement
                ? MapReplacementPost
                : CreatePost)
            .Where(static mapping => mapping.IsResultDependent)
            .ToImmutableArray();

        return new ConstructorInitializationMappingPlan(
            initializerMappings,
            postMappings,
            Observation.RequiredObligations,
            Observation.Rules.Where(static rule =>
                    rule.InvalidReason == MemberRuleInvalidReason.None &&
                    rule.Lifecycle.HasFlag(
                        MemberLifecycleDependency.Creation) &&
                    rule.Lifecycle.HasFlag(
                        MemberLifecycleDependency.Result) &&
                    !rule.Lifecycle.HasFlag(
                        MemberLifecycleDependency.ExistingDestination))
                .ToImmutableArray(),
            Observation);
    }
}

internal readonly record struct ConstructorInitializationMappingPlan(
    ImmutableArray<TypeMapperMemberMappingModel> InitializerMappings,
    ImmutableArray<TypeMapperMemberMappingModel> PostMappings,
    ImmutableArray<ISymbol> RequiredObligations,
    ImmutableArray<MemberRuleObservation>
        ResultDependentCreationOnlyRules,
    MemberPlanningObservation Observation);

internal readonly record struct ConventionReadableMember(
    string Name,
    ITypeSymbol Type,
    ISymbol Symbol,
    ConventionSourceAccessModel? SourceAccess = null,
    string? PathDisplay = null,
    string? PathIdentity = null,
    ImmutableArray<ISymbol> SourcePathMembers = default)
{
    public ConventionSourceValueExpressionModel?
        BuildConventionValueExpression(string sourceName) =>
        SourceAccess?.BuildValueExpression(
            sourceName,
            Symbol,
            Type);

    public ImmutableArray<ISymbol> GetSourcePathMembers() =>
        SourcePathMembers.IsDefaultOrEmpty
            ? ImmutableArray.Create(Symbol)
            : SourcePathMembers;

    public string GetPathDisplay() => PathDisplay ?? Name;
}

internal readonly record struct ConventionWritableMember(
    string Name,
    ITypeSymbol Type,
    bool CanAssign,
    bool IsRequired,
    ISymbol Symbol);
