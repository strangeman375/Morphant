using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class BclTupleMappingPlanner
{
    public static ConventionConstructorPlanningResult BuildConvention(
        BclTupleShape destination,
        ITypeSymbol sourceType,
        ConventionSourceMemberContext sourceContext,
        ConstructorInitializationMappingPlan memberMappings,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        string nonNullSourceName,
        bool useMemberConvention,
        CancellationToken cancellationToken)
    {
        var observation = EmptyObservation();
        var initialArguments =
            ImmutableDictionary.CreateBuilder<
                string,
                TypeMapperConstructorArgumentMappingModel>(
                StringComparer.Ordinal);

        foreach (var element in useMemberConvention
                     ? destination.Elements
                     : ImmutableArray<BclTupleElement>.Empty)
        {
            var automatic = TryBuildAutomaticArgument(
                sourceType,
                sourceContext,
                destination,
                element,
                parameter: null,
                compilation,
                mapperType,
                nonNullSourceName,
                originNode: null,
                cancellationToken,
                out _);

            if (automatic is { } argument)
            {
                initialArguments.Add(element.Name, argument);
            }
        }

        return BuildFinalPlan(
            destination,
            initialArguments.ToImmutable(),
            memberMappings,
            observation,
            mapperType,
            preserveIgnoredInitialValues: false);
    }

    public static ConventionConstructorPlanningResult BuildStructured(
        BclTupleShape destination,
        ITypeSymbol sourceType,
        ConventionSourceMemberContext sourceContext,
        ConstructorInitializationMappingPlan memberMappings,
        BaseObjectCreationExpressionSyntax creation,
        ImmutableArray<StructuredObjectArgument> objectArguments,
        SemanticModel semanticModel,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        string nonNullSourceName,
        Func<ExpressionSyntax, IParameterSymbol,
            TypeMapperRewrittenDependencyExpression?>
            rewriteDependencyExpression,
        CancellationToken cancellationToken)
    {
        var logicalConstructor = FindLogicalConstructor(
            creation,
            destination,
            semanticModel,
            cancellationToken);

        if (logicalConstructor is null)
        {
            return new ConventionConstructorPlanningResult(
                Plan: null,
                EmptyObservation());
        }

        var initialArguments =
            ImmutableDictionary.CreateBuilder<
                string,
                TypeMapperConstructorArgumentMappingModel>(
                StringComparer.Ordinal);
        var parameterObservations =
            ImmutableArray.CreateBuilder<
                ConstructorParameterRuleObservation>();
        var rejection = ConstructorCandidateRejectionReason.None;

        void Reject(ConstructorCandidateRejectionReason reason)
        {
            if (rejection == ConstructorCandidateRejectionReason.None)
            {
                rejection = reason;
            }
        }

        void AddRule(
            BclTupleElement element,
            IParameterSymbol parameter,
            ExpressionSyntax value,
            SyntaxNode designator,
            ConstructorParameterRuleOrigin configuredOrigin,
            int declarativeOrder)
        {
            if (DeclarativeConstructorMarker.TryGetKind(
                    value,
                    element.Type,
                    semanticModel,
                    mapperType,
                    cancellationToken,
                    out var markerKind))
            {
                if (markerKind ==
                    DeclarativeConstructorMarkerKind.Ignore)
                {
                    initialArguments[element.Name] =
                        DefaultArgument(
                            element,
                            parameter,
                            value,
                            declarativeOrder);
                    parameterObservations.Add(
                        BuildParameterObservation(
                            element,
                            parameter,
                            ConstructorParameterRuleOrigin.Ignore,
                            value,
                            sourceMember: null,
                            designator,
                            isApplicable: true,
                            ConstructorCandidateRejectionReason.None));
                    return;
                }

                if (markerKind == DeclarativeConstructorMarkerKind.Auto)
                {
                    var automatic = TryBuildAutomaticArgument(
                        sourceType,
                        sourceContext,
                        destination,
                        element,
                        parameter,
                        compilation,
                        mapperType,
                        nonNullSourceName,
                        value,
                        cancellationToken,
                        out var sourceMember);

                    if (automatic is not { } automaticArgument)
                    {
                        var suppliedByMembers = IsSuppliedByMemberInitializer(
                            element,
                            memberMappings);
                        parameterObservations.Add(
                            BuildParameterObservation(
                                element,
                                parameter,
                                ConstructorParameterRuleOrigin.Auto,
                                value,
                                sourceMember?.Symbol,
                                designator,
                                isApplicable: false,
                                suppliedByMembers
                                    ? ConstructorCandidateRejectionReason.None
                                    : ConstructorCandidateRejectionReason
                                        .MissingSourceMember));

                        if (!suppliedByMembers)
                        {
                            Reject(ConstructorCandidateRejectionReason
                                .MissingSourceMember);
                        }

                        return;
                    }

                    initialArguments[element.Name] =
                        automaticArgument with
                        {
                            DeclarativeOrder = declarativeOrder
                        };
                    parameterObservations.Add(
                        BuildParameterObservation(
                            element,
                            parameter,
                            ConstructorParameterRuleOrigin.Auto,
                            value,
                            sourceMember?.Symbol,
                            designator,
                            isApplicable: true,
                            ConstructorCandidateRejectionReason.None));
                    return;
                }
            }

            var rewritten = rewriteDependencyExpression(value, parameter);

            if (rewritten is null)
            {
                parameterObservations.Add(BuildParameterObservation(
                    element,
                    parameter,
                    configuredOrigin,
                    value,
                    sourceMember: null,
                    designator,
                    isApplicable: false,
                    ConstructorCandidateRejectionReason.ExplicitRule));
                Reject(ConstructorCandidateRejectionReason.ExplicitRule);
                return;
            }

            var referencedSymbol = semanticModel.GetSymbolInfo(
                    value,
                    cancellationToken)
                .Symbol;
            var sourceMemberSymbol = referencedSymbol is
                IPropertySymbol or IFieldSymbol
                    ? referencedSymbol
                    : null;
            initialArguments[element.Name] =
                new TypeMapperConstructorArgumentMappingModel(
                    element.Name,
                    SourceMemberName: string.Empty,
                    ValueLocalName: null,
                    rewritten.Value.Expression,
                    ValueLocalTypeName: BuildElementTypeName(element),
                    TargetTypeName: BuildElementTypeName(element),
                    DependencyExpression:
                        rewritten.Value.DependencyExpression,
                    ParameterSymbol: parameter,
                    SourceMemberSymbol: sourceMemberSymbol,
                    RuleOriginNode: value,
                    RuleOrigin: configuredOrigin,
                    TupleElementOrdinal: element.Ordinal,
                    DeclarativeOrder: declarativeOrder);
            parameterObservations.Add(BuildParameterObservation(
                element,
                parameter,
                configuredOrigin,
                value,
                sourceMemberSymbol,
                designator,
                isApplicable: true,
                ConstructorCandidateRejectionReason.None));
        }

        if (StructuredConstructMappingPlanner.TryGetByConventionRules(
                objectArguments,
                destination.Type,
                compilation,
                semanticModel,
                cancellationToken,
                out var conventionRules))
        {
            var rules = conventionRules.ToDictionary(
                static rule => rule.ParameterName,
                StringComparer.Ordinal);

            foreach (var element in destination.Elements)
            {
                var parameter = FindParameter(
                    logicalConstructor,
                    element.Name);

                if (parameter is null)
                {
                    Reject(ConstructorCandidateRejectionReason
                        .InvocationBinding);
                    continue;
                }

                if (rules.TryGetValue(element.Name, out var rule))
                {
                    AddRule(
                        element,
                        parameter,
                        rule.Value,
                        rule.DesignatorNode,
                        ConstructorParameterRuleOrigin.Value,
                        destination.Elements.Length +
                        conventionRules.IndexOf(rule));
                    continue;
                }

                var automatic = TryBuildAutomaticArgument(
                    sourceType,
                    sourceContext,
                    destination,
                    element,
                    parameter,
                    compilation,
                    mapperType,
                    nonNullSourceName,
                    originNode: null,
                    cancellationToken,
                    out var sourceMember);

                if (automatic is { } automaticArgument)
                {
                    initialArguments[element.Name] = automaticArgument;
                    parameterObservations.Add(BuildParameterObservation(
                        element,
                        parameter,
                        ConstructorParameterRuleOrigin.Convention,
                        originNode: null,
                        sourceMember?.Symbol,
                        designatorNode: null,
                        isApplicable: true,
                        ConstructorCandidateRejectionReason.None));
                }
                else
                {
                    var suppliedByMembers = IsSuppliedByMemberInitializer(
                        element,
                        memberMappings);
                    parameterObservations.Add(BuildParameterObservation(
                        element,
                        parameter,
                        ConstructorParameterRuleOrigin.Convention,
                        originNode: null,
                        sourceMember?.Symbol,
                        designatorNode: null,
                        isApplicable: false,
                        suppliedByMembers
                            ? ConstructorCandidateRejectionReason.None
                            : ConstructorCandidateRejectionReason
                                .MissingSourceMember));

                    if (!suppliedByMembers)
                    {
                        Reject(ConstructorCandidateRejectionReason
                            .MissingSourceMember);
                    }
                }
            }
        }
        else
        {
            for (var index = 0;
                 index < objectArguments.Length;
                 index++)
            {
                var argument = objectArguments[index];
                var parameterName = argument.Syntax.NameColon?.Name
                    .Identifier.ValueText ??
                    (index < logicalConstructor.Parameters.Length
                        ? logicalConstructor.Parameters[index].Name
                        : string.Empty);
                var element = destination.Elements.FirstOrDefault(
                    candidate => StringComparer.Ordinal.Equals(
                        candidate.Name,
                        parameterName));
                var parameter = FindParameter(
                    logicalConstructor,
                    parameterName);

                if (element is null || parameter is null ||
                    initialArguments.ContainsKey(parameterName))
                {
                    Reject(ConstructorCandidateRejectionReason
                        .ExplicitRule);
                    continue;
                }

                AddRule(
                    element,
                    parameter,
                    argument.Value,
                    (SyntaxNode?)argument.Syntax.NameColon?.Name ??
                    argument.Syntax,
                    ConstructorParameterRuleOrigin.Value,
                    index);
            }

            foreach (var element in destination.Elements)
            {
                if (initialArguments.ContainsKey(element.Name) ||
                    IsSuppliedByMemberInitializer(
                        element,
                        memberMappings))
                {
                    continue;
                }

                var parameter = FindParameter(
                    logicalConstructor,
                    element.Name);
                parameterObservations.Add(BuildParameterObservation(
                    element,
                    parameter,
                    ConstructorParameterRuleOrigin.Omitted,
                    originNode: null,
                    sourceMember: null,
                    designatorNode: null,
                    isApplicable: false,
                    ConstructorCandidateRejectionReason.ExplicitRule));
                Reject(ConstructorCandidateRejectionReason.ExplicitRule);
            }
        }

        var candidate = new ConstructorCandidateObservation(
            logicalConstructor,
            parameterObservations.ToImmutable(),
            rejection);
        var observation = new ConstructorPlanningObservation(
            ConstructorSelectionValue.Explicit,
            StrategyOrigin: null,
            ImmutableArray.Create(candidate),
            rejection == ConstructorCandidateRejectionReason.None
                ? logicalConstructor
                : null,
            Terminals: ImmutableArray<StructuredTerminalObservation>.Empty);

        return rejection != ConstructorCandidateRejectionReason.None
            ? new ConventionConstructorPlanningResult(
                Plan: null,
                observation)
            : BuildFinalPlan(
                destination,
                initialArguments.ToImmutable(),
                memberMappings,
                observation,
                mapperType,
                preserveIgnoredInitialValues: true);
    }

    private static ConventionConstructorPlanningResult BuildFinalPlan(
        BclTupleShape destination,
        ImmutableDictionary<
            string,
            TypeMapperConstructorArgumentMappingModel> initialArguments,
        ConstructorInitializationMappingPlan memberMappings,
        ConstructorPlanningObservation observation,
        INamedTypeSymbol mapperType,
        bool preserveIgnoredInitialValues)
    {
        var arguments =
            ImmutableArray.CreateBuilder<
                TypeMapperConstructorArgumentMappingModel>(
                destination.Elements.Length);
        var survivingInitialElements = new HashSet<string>(
            StringComparer.Ordinal);

        var memberOrderBase = initialArguments.IsEmpty
            ? 0
            : initialArguments.Values.Max(static argument =>
                argument.DeclarativeOrder) + 1;

        foreach (var element in destination.Elements)
        {
            initialArguments.TryGetValue(
                element.Name,
                out var initialArgument);
            var hasInitial = !String.IsNullOrEmpty(
                initialArgument.ParameterName);
            var memberMapping = memberMappings.InitializerMappings
                .LastOrDefault(candidate => StringComparer.Ordinal.Equals(
                    candidate.DestinationMemberName,
                    element.Name));
            var hasMemberMapping = !String.IsNullOrEmpty(
                memberMapping.DestinationMemberName);
            var memberOrigin = FindMemberRuleOrigin(
                element,
                memberMappings.Observation);

            if (memberOrigin == MemberRuleOrigin.Ignore)
            {
                arguments.Add(hasInitial && preserveIgnoredInitialValues
                    ? initialArgument
                    : DefaultArgument(
                        element,
                        declarativeOrder: memberOrderBase +
                            memberMappings.InitializerMappings.Length +
                            element.Ordinal));

                if (hasInitial && preserveIgnoredInitialValues)
                {
                    survivingInitialElements.Add(element.Name);
                }

                continue;
            }

            if (hasMemberMapping &&
                (!hasInitial || memberOrigin != MemberRuleOrigin.Convention))
            {
                var memberIndex = memberMappings.InitializerMappings
                    .IndexOf(memberMapping);
                arguments.Add(ToArgument(
                    element,
                    memberMapping,
                    memberOrderBase + Math.Max(memberIndex, 0)));
                continue;
            }

            if (hasInitial)
            {
                arguments.Add(initialArgument);
                survivingInitialElements.Add(element.Name);
                continue;
            }

            return new ConventionConstructorPlanningResult(
                Plan: null,
                observation);
        }

        var orderedArguments = arguments
            .OrderBy(static argument => argument.DeclarativeOrder)
            .ThenBy(static argument => argument.TupleElementOrdinal)
            .ToArray();
        var requiresOrderLocals = !orderedArguments
            .Select(static argument => argument.TupleElementOrdinal)
            .SequenceEqual(
                orderedArguments
                    .Select(static argument => argument.TupleElementOrdinal)
                    .OrderBy(static ordinal => ordinal));

        if (requiresOrderLocals)
        {
            var usedNames =
                ConventionConstructorMappingPlanner.BuildUsedValueLocalNames(
                    mapperType);

            for (var index = 0; index < orderedArguments.Length; index++)
            {
                var argument = orderedArguments[index];
                orderedArguments[index] = argument with
                {
                    ValueLocalName = argument.ValueLocalName ??
                        ConventionConstructorMappingPlanner
                            .MakeUniqueValueLocalName(
                                "tuple",
                                argument.ParameterName,
                                usedNames),
                    ValueLocalTypeName = argument.ValueLocalTypeName ??
                        argument.TargetTypeName
                };
            }
        }

        var tupleConstruction = new TypeMapperTupleConstructionModel(
            destination.Kind,
            destination.Elements.Select(BuildElementTypeName)
                .ToImmutableArray(),
            destination.Elements.Select(static element =>
                    element.SemanticName)
                .ToImmutableArray());
        var constructor = new TypeMapperConstructorMappingModel(
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                destination.Type),
            orderedArguments.ToImmutableArray(),
            TupleConstruction: tupleConstruction);
        var reconstruction = destination.IsValueTuple ||
                             memberMappings.PostMappings.IsEmpty
            ? (TypeMapperTupleReconstructionModel?)null
            : new TypeMapperTupleReconstructionModel(
                tupleConstruction,
                destination.Elements.Select(element =>
                        new TypeMapperTupleElementModel(
                            element.Name,
                            element.AccessPath))
                    .ToImmutableArray());
        var postMappings = memberMappings.PostMappings;

        if (reconstruction is not null)
        {
            var postOrdinals = postMappings.Select(mapping =>
                    destination.Elements.First(element =>
                        StringComparer.Ordinal.Equals(
                            element.Name,
                            mapping.DestinationMemberName)).Ordinal)
                .ToImmutableArray();

            if (!postOrdinals.SequenceEqual(
                    postOrdinals.OrderBy(static ordinal => ordinal)))
            {
                var usedNames =
                    ConventionConstructorMappingPlanner
                        .BuildUsedValueLocalNames(mapperType);

                foreach (var argument in orderedArguments)
                {
                    if (argument.ValueLocalName is { } valueLocalName)
                    {
                        usedNames.Add(valueLocalName);
                    }
                }

                postMappings = postMappings.Select(mapping =>
                        mapping with
                        {
                            ValueLocalName = mapping.ValueLocalName ??
                                ConventionConstructorMappingPlanner
                                    .MakeUniqueValueLocalName(
                                        "tupleFinal",
                                        mapping.DestinationMemberName,
                                        usedNames)
                        })
                    .ToImmutableArray();
            }
        }

        var finalObservation = KeepSurvivingInitialRules(
            observation,
            survivingInitialElements);

        return new ConventionConstructorPlanningResult(
            new ConventionConstructorMappingPlan(
                constructor,
                CreateMemberMappings:
                    ImmutableArray<TypeMapperMemberMappingModel>.Empty,
                postMappings,
                finalObservation,
                reconstruction),
            finalObservation);
    }

    private static ConstructorPlanningObservation
        KeepSurvivingInitialRules(
            ConstructorPlanningObservation observation,
            ISet<string> survivingElements)
    {
        if (observation.Candidates.IsEmpty)
        {
            return observation;
        }

        return observation with
        {
            Candidates = observation.Candidates.Select(candidate =>
                    candidate with
                    {
                        ParameterRules = candidate.ParameterRules
                            .Select(rule => survivingElements.Contains(
                                    rule.ParameterName)
                                ? rule
                                : rule with
                                {
                                    IsApplicable = false
                                })
                            .ToImmutableArray()
                    })
                .ToImmutableArray()
        };
    }

    private static TypeMapperConstructorArgumentMappingModel?
        TryBuildAutomaticArgument(
            ITypeSymbol sourceType,
            ConventionSourceMemberContext sourceContext,
            BclTupleShape destination,
            BclTupleElement element,
            IParameterSymbol? parameter,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            string nonNullSourceName,
            SyntaxNode? originNode,
            CancellationToken cancellationToken,
            out ConventionReadableMember? selectedSourceMember)
    {
        selectedSourceMember = null;

        if (!element.HasSemanticName)
        {
            return null;
        }

        var resolution = ConventionSourceMemberResolver.ResolveConstructor(
            sourceContext,
            element.Name,
            compilation,
            mapperType,
            cancellationToken);
        var compatible = FindCompatibleSourceMembers(
            sourceType,
            destination.Type,
            element,
            resolution.Candidates,
            compilation,
            mapperType,
            cancellationToken);

        if (!resolution.HasDirectClaim &&
            compatible.IsEmpty &&
            !resolution.FallbackCandidates.IsEmpty)
        {
            compatible = FindCompatibleSourceMembers(
                sourceType,
                destination.Type,
                element,
                resolution.FallbackCandidates,
                compilation,
                mapperType,
                cancellationToken);
        }

        if (compatible.Length != 1)
        {
            return null;
        }

        selectedSourceMember = compatible[0];

        return new TypeMapperConstructorArgumentMappingModel(
            element.Name,
            selectedSourceMember.Value.Name,
            ValueLocalName: null,
            ConventionValueExpression:
                selectedSourceMember.Value.BuildConventionValueExpression(
                    nonNullSourceName),
            ConventionProbeValueExpression:
                selectedSourceMember.Value.BuildConventionValueExpression(
                    "source!"),
            TargetTypeName: BuildElementTypeName(element),
            ParameterSymbol: parameter,
            SourceMemberSymbol: selectedSourceMember.Value.Symbol,
            RuleOriginNode: originNode,
            RuleOrigin: originNode is null
                ? ConstructorParameterRuleOrigin.Convention
                : ConstructorParameterRuleOrigin.Auto,
            TupleElementOrdinal: element.Ordinal,
            DeclarativeOrder: element.Ordinal - 1,
            SourcePathMembers:
                selectedSourceMember.Value.GetSourcePathMembers());
    }

    private static ImmutableArray<ConventionReadableMember>
        FindCompatibleSourceMembers(
            ITypeSymbol sourceType,
            ITypeSymbol destinationType,
            BclTupleElement element,
            ImmutableArray<ConventionReadableMember> candidates,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            CancellationToken cancellationToken)
    {
        if (candidates.IsEmpty)
        {
            return ImmutableArray<ConventionReadableMember>.Empty;
        }

        var compatibility = MemberTypeCompatibility.FindCompatibleCandidates(
            sourceType,
            destinationType,
            candidates.Select(candidate =>
                    new MemberTypeCompatibilityCandidate(
                        candidate.Name,
                        element.Name,
                        candidate.Type,
                        element.Type,
                        CanAssign: false,
                        candidate.BuildConventionValueExpression("source!"),
                        UseValueProbe: true))
                .ToImmutableArray(),
            compilation,
            mapperType,
            cancellationToken);

        return candidates.Where((_, index) => compatibility[index])
            .ToImmutableArray();
    }

    private static IMethodSymbol? FindLogicalConstructor(
        BaseObjectCreationExpressionSyntax creation,
        BclTupleShape destination,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var selected = semanticModel.GetSymbolInfo(
                creation,
                cancellationToken)
            .Symbol as IMethodSymbol;
        var planType = selected?.ContainingType ??
            semanticModel.GetTypeInfo(creation, cancellationToken)
                .Type as INamedTypeSymbol;

        bool Matches(IMethodSymbol constructor) =>
            constructor.Parameters.Length == destination.Elements.Length &&
            constructor.Parameters.Select(static parameter => parameter.Name)
                .SequenceEqual(
                    destination.Elements.Select(static element =>
                        element.Name),
                    StringComparer.Ordinal);

        return selected is not null && Matches(selected)
            ? selected
            : planType?.InstanceConstructors.FirstOrDefault(Matches);
    }

    private static IParameterSymbol? FindParameter(
        IMethodSymbol constructor,
        string name) =>
        constructor.Parameters.FirstOrDefault(parameter =>
            StringComparer.Ordinal.Equals(parameter.Name, name));

    private static ConstructorParameterRuleObservation
        BuildParameterObservation(
            BclTupleElement element,
            IParameterSymbol? parameter,
            ConstructorParameterRuleOrigin origin,
            SyntaxNode? originNode,
            ISymbol? sourceMember,
            SyntaxNode? designatorNode,
            bool isApplicable,
            ConstructorCandidateRejectionReason rejection) =>
        new(
            parameter,
            element.Name,
            origin,
            originNode,
            sourceMember,
            element.Symbol,
            isApplicable,
            rejection,
            designatorNode,
            SourcePathMembers: sourceMember is null
                ? default
                : ImmutableArray.Create(sourceMember));

    private static MemberRuleOrigin? FindMemberRuleOrigin(
        BclTupleElement element,
        MemberPlanningObservation observation) =>
        observation.Rules.LastOrDefault(rule =>
            rule.InvalidReason == MemberRuleInvalidReason.None &&
            (SymbolEqualityComparer.Default.Equals(
                 rule.DestinationMember,
                 element.Symbol) ||
             StringComparer.Ordinal.Equals(
                 rule.DestinationMember.Name,
                 element.Name)))?.Origin;

    private static bool IsSuppliedByMemberInitializer(
        BclTupleElement element,
        ConstructorInitializationMappingPlan memberMappings)
    {
        return memberMappings.InitializerMappings.Any(mapping =>
                   StringComparer.Ordinal.Equals(
                       mapping.DestinationMemberName,
                       element.Name)) &&
               FindMemberRuleOrigin(element, memberMappings.Observation) is
                   not MemberRuleOrigin.Convention;
    }

    private static TypeMapperConstructorArgumentMappingModel ToArgument(
        BclTupleElement element,
        TypeMapperMemberMappingModel mapping,
        int declarativeOrder)
    {
        var explicitValueExpression = mapping.ExplicitValueExpression;

        if (mapping.SourceValueLocalName is not null &&
            explicitValueExpression is null &&
            mapping.ConventionValueExpression is null)
        {
            explicitValueExpression = mapping.SourceValueLocalName;
        }

        return new TypeMapperConstructorArgumentMappingModel(
            element.Name,
            mapping.SourceMemberName,
            mapping.ValueLocalName,
            explicitValueExpression,
            mapping.ConventionValueExpression,
            ConventionProbeValueExpression: null,
            ValueLocalTypeName: mapping.ValueLocalName is null
                ? null
                : mapping.ExplicitValueTypeName,
            TargetTypeName: BuildElementTypeName(element),
            mapping.DependencyExpression,
            mapping.EvaluationLocals,
            ParameterSymbol: null,
            SourceMemberSymbol: null,
            RuleOriginNode: null,
            RuleOrigin: null,
            TupleElementOrdinal: element.Ordinal,
            DeclarativeOrder: declarativeOrder);
    }

    private static TypeMapperConstructorArgumentMappingModel DefaultArgument(
        BclTupleElement element,
        IParameterSymbol? parameter = null,
        SyntaxNode? originNode = null,
        int? declarativeOrder = null)
    {
        var typeName = BuildElementTypeName(element);

        return new TypeMapperConstructorArgumentMappingModel(
            element.Name,
            SourceMemberName: string.Empty,
            ValueLocalName: null,
            ExplicitValueExpression: $"default({typeName})",
            ValueLocalTypeName: typeName,
            TargetTypeName: typeName,
            ParameterSymbol: parameter,
            RuleOriginNode: originNode,
            RuleOrigin: ConstructorParameterRuleOrigin.Ignore,
            TupleElementOrdinal: element.Ordinal,
            DeclarativeOrder: declarativeOrder ?? element.Ordinal - 1);
    }

    private static string BuildElementTypeName(BclTupleElement element) =>
        TypeMapperMappingTypePolicy.GetGeneratedTypeName(element.Type);

    private static ConstructorPlanningObservation EmptyObservation() =>
        new(
            Strategy: null,
            StrategyOrigin: null,
            Candidates: ImmutableArray<ConstructorCandidateObservation>.Empty,
            SelectedConstructor: null,
            Terminals: ImmutableArray<StructuredTerminalObservation>.Empty);
}
