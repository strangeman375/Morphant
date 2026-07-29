using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MapperBuilderMap;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TypeMapperPipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValuesProvider<MapperBuilderMapInfo> mapInfos)
    {
        var requests = mapInfos
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                TryBuildGenerationInput(source, cancellationToken))
            .WhereHasValue()
            .Collect()
            .SelectMany(static (generationInputs, cancellationToken) =>
                BuildRequests(
                    generationInputs,
                    cancellationToken))
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildTypeMappers);

        context.RegisterSourceOutput(
            requests,
            static (context, request) =>
                context.AddSource(
                    request.HintName,
                    TypeMapperEmitter.Emit(request.Model)));
    }

    private static TypeMapperGenerationInput? TryBuildGenerationInput(
        (
            MapperBuilderMapInfo MapInfo,
            CompilationContext Context
        ) source,
        CancellationToken cancellationToken)
    {
        var (mapInfo, context) = source;

        var semanticModel = context.Compilation.GetSemanticModel(
            mapInfo.ConfigureSyntax.SyntaxTree);

        if (mapInfo.ConfigureSyntax.Parent is not ClassDeclarationSyntax mapperDeclaration ||
            semanticModel.GetDeclaredSymbol(
                mapperDeclaration,
                cancellationToken) is not INamedTypeSymbol mapperType ||
            !CanGenerate(
                mapperType,
                mapperDeclaration))
        {
            return null;
        }

        var mappings = BuildMappings(
            mapInfo,
            context.Compilation,
            mapperType,
            cancellationToken);

        if (mappings.IsDefaultOrEmpty)
        {
            return null;
        }

        var mapperNamespace =
            mapperType.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : mapperType.ContainingNamespace.ToDisplayString();

        var model = new TypeMapperModel(
            mapperNamespace,
            BuildContainingTypes(mapperDeclaration),
            GetAccessibility(mapperType.DeclaredAccessibility),
            mapperDeclaration.Identifier.Text,
            BuildTypeParameterList(
                mapperDeclaration.TypeParameterList),
            mappings);

        return new TypeMapperGenerationInput(
            SymbolNameHelper.GetFullMetadataName(mapperType),
            model);
    }

    private static ImmutableArray<TypeMapperRequest> BuildRequests(
        ImmutableArray<TypeMapperGenerationInput> generationInputs,
        CancellationToken cancellationToken)
    {
        var orderedInputs = generationInputs.ToArray();

        Array.Sort(
            orderedInputs,
            static (left, right) =>
                StringComparer.Ordinal.Compare(
                    left.StableIdentity,
                    right.StableIdentity));

        var hintNamePartAllocator = new HintNamePartAllocator();
        var requests =
            ImmutableArray.CreateBuilder<TypeMapperRequest>(
                orderedInputs.Length);

        foreach (var generationInput in orderedInputs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hintName = GeneratedSourceHintName.Create(
                "TypeMapper",
                hintNamePartAllocator.Allocate(
                    generationInput.StableIdentity));

            requests.Add(
                new TypeMapperRequest(
                    hintName,
                    generationInput.Model));
        }

        return requests.ToImmutable();
    }

    private static bool CanGenerate(
        INamedTypeSymbol mapperType,
        ClassDeclarationSyntax mapperDeclaration)
    {
        if (!IsPartial(mapperDeclaration) ||
            !IsSupportedAccessibility(
                mapperType.DeclaredAccessibility) ||
            mapperDeclaration
                .Ancestors()
                .OfType<TypeDeclarationSyntax>()
                .Any(static declaration =>
                    !IsPartial(declaration)))
        {
            return false;
        }

        for (var current = mapperType;
             current is not null;
             current = current.ContainingType)
        {
            if (current.IsFileLocal)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPartial(
        TypeDeclarationSyntax declaration)
    {
        return declaration.Modifiers.Any(
            SyntaxKind.PartialKeyword);
    }

    private static bool IsSupportedAccessibility(
        Accessibility accessibility)
    {
        return accessibility is
            Accessibility.Public or
            Accessibility.Internal or
            Accessibility.Private or
            Accessibility.Protected or
            Accessibility.ProtectedAndInternal or
            Accessibility.ProtectedOrInternal;
    }

    private static ImmutableArray<TypeMapperContainingTypeModel>
        BuildContainingTypes(
            ClassDeclarationSyntax mapperDeclaration)
    {
        return mapperDeclaration
            .Ancestors()
            .OfType<TypeDeclarationSyntax>()
            .Reverse()
            .Select(static declaration =>
                new TypeMapperContainingTypeModel(
                    GetDeclarationKind(declaration),
                    declaration.Identifier.Text,
                    BuildTypeParameterList(
                        declaration.TypeParameterList)))
            .ToImmutableArray();
    }

    private static string GetDeclarationKind(
        TypeDeclarationSyntax declaration)
    {
        if (declaration is RecordDeclarationSyntax recordDeclaration)
        {
            return recordDeclaration.ClassOrStructKeyword.IsKind(
                SyntaxKind.StructKeyword)
                    ? "record struct"
                    : "record";
        }

        return declaration switch
        {
            ClassDeclarationSyntax => "class",
            StructDeclarationSyntax => "struct",
            InterfaceDeclarationSyntax => "interface",
            _ => throw new InvalidOperationException(
                $"Unsupported containing type declaration: {declaration.Kind()}.")
        };
    }

    private static string BuildTypeParameterList(
        TypeParameterListSyntax? typeParameterList)
    {
        if (typeParameterList is null)
        {
            return string.Empty;
        }

        return
            "<" +
            string.Join(
                ", ",
                typeParameterList.Parameters.Select(
                    static parameter =>
                        parameter.Identifier.Text)) +
            ">";
    }

    private static ImmutableArray<TypeMapperMappingModel> BuildMappings(
        MapperBuilderMapInfo mapInfo,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var registrations =
            ImmutableArray.CreateBuilder<
                MapperBuilderMapRegistrationInfo>();

        foreach (var registration in mapInfo.Registrations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!MappingTypePolicy.IsSupported(
                    registration.SourceType) ||
                !MappingTypePolicy.IsSupported(
                    registration.DestinationType) ||
                registrations.Any(
                    existing =>
                        TypeMapperMappingTypePolicy.AreEquivalent(
                            existing.SourceType,
                            registration.SourceType) &&
                        TypeMapperMappingTypePolicy.AreEquivalent(
                            existing.DestinationType,
                            registration.DestinationType)))
            {
                continue;
            }

            registrations.Add(registration);
        }

        for (var leftIndex = 0;
             leftIndex < registrations.Count;
             leftIndex++)
        {
            for (var rightIndex = leftIndex + 1;
                 rightIndex < registrations.Count;
                 rightIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var left = registrations[leftIndex];
                var right = registrations[rightIndex];

                if (TypeMapperMappingTypePolicy.CanMappingsUnify(
                        left.SourceType,
                        left.DestinationType,
                        right.SourceType,
                        right.DestinationType))
                {
                    return default;
                }
            }
        }

        return registrations
            .Select(registration =>
                BuildMapping(
                    registration,
                    compilation,
                    mapperType,
                    cancellationToken))
            .ToImmutableArray();
    }

    private static TypeMapperMappingModel BuildMapping(
        MapperBuilderMapRegistrationInfo registration,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var destinationPlan =
            BuildDestinationPlan(
                registration.DestinationType,
                cancellationToken);

        var conventionMemberMappings = ConventionMemberMappingPlanner.Build(
            registration.SourceType,
            destinationPlan.MemberType,
            compilation,
            mapperType,
            cancellationToken);
        var templateMappingResult = TemplateMappingPlanner.Build(
            registration,
            destinationPlan.MemberType,
            compilation,
            mapperType,
            cancellationToken);

        if (templateMappingResult is null)
        {
            return BuildFlatMapping(
                registration,
                destinationPlan,
                conventionMemberMappings,
                templateMapping: null,
                runtimeLocals: [],
                compilation,
                mapperType,
                cancellationToken);
        }

        if (templateMappingResult is
            UnsupportedTemplateMappingPlanResult unsupported)
        {
            return BuildUnsupportedMapping(
                registration,
                destinationPlan,
                unsupported.Message);
        }

        var templateMapping =
            (SupportedTemplateMappingPlanResult)
            templateMappingResult;
        var plannedRoot = BuildPlannedControlFlow(
            templateMapping.Root,
            leaf => BuildFlatMapping(
                registration,
                destinationPlan,
                conventionMemberMappings,
                leaf,
                templateMapping.RuntimeLocals,
                compilation,
                mapperType,
                cancellationToken));
        var runtimeLocalsByPlaceholder =
            templateMapping.RuntimeLocals.ToDictionary(
                static local => local.PlaceholderName,
                StringComparer.Ordinal);
        var mapNewRoot =
            HoistConditionalConstructorValues(
                BuildModeControlFlow(
                    plannedRoot,
                    runtimeLocalsByPlaceholder,
                    mapNew: true),
                mapperType);
        var mapExistingRoot = BuildModeControlFlow(
            plannedRoot,
            runtimeLocalsByPlaceholder,
            mapNew: false);
        var controlFlow = BuildControlFlowModel(
            templateMapping.RuntimeLocals,
            mapNewRoot,
            mapExistingRoot,
            mapperType);

        if (controlFlow.MapNewRoot.Locals.IsEmpty &&
            controlFlow.MapExistingRoot.Locals.IsEmpty &&
            controlFlow.MapNewRoot.Leaf is { } mapNewLeaf &&
            controlFlow.MapExistingRoot.Leaf is
                { } mapExistingLeaf)
        {
            return CombineModeMappings(
                mapNewLeaf,
                mapExistingLeaf);
        }

        var representative =
            FindFirstLeaf(mapNewRoot) ??
            FindFirstLeaf(mapExistingRoot) ??
            BuildFlatMapping(
                registration,
                destinationPlan,
                conventionMemberMappings,
                templateMapping: null,
                runtimeLocals: [],
                compilation,
                mapperType,
                cancellationToken);

        return representative with
        {
            ControlFlow = controlFlow
        };
    }

    private static TypeMapperMappingModel BuildUnsupportedMapping(
        MapperBuilderMapRegistrationInfo registration,
        DestinationPlan destinationPlan,
        string exceptionMessage)
    {
        return new TypeMapperMappingModel(
            SourceTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    registration.SourceType),
            MaybeNullSourceTypeName:
                TypeMapperMappingTypePolicy
                    .GetGeneratedMaybeNullTypeName(
                        registration.SourceType),
            DestinationTypeName:
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    registration.DestinationType),
            MaybeNullDestinationTypeName:
                TypeMapperMappingTypePolicy
                    .GetGeneratedMaybeNullTypeName(
                        registration.DestinationType),
            MapNewDirectExpression: null,
            MapExistingDirectExpression: null,
            MapNewFactory: null,
            MapNewConstructor: null,
            MapExistingKind: destinationPlan.MapExistingKind,
            MapExistingDestinationLocalName: null,
            MapNewMemberMappings: [],
            MapExistingMemberMappings: [],
            ControlFlow: null,
            UnsupportedExceptionMessage: exceptionMessage);
    }

    private static TypeMapperMappingModel BuildFlatMapping(
        MapperBuilderMapRegistrationInfo registration,
        DestinationPlan destinationPlan,
        ConventionMemberMappingPlan conventionMemberMappings,
        TemplateMappingPlan? templateMapping,
        ImmutableArray<TemplateRuntimeLocalPlan> runtimeLocals,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var memberMappings = MergeMemberMappings(
            conventionMemberMappings,
            templateMapping,
            destinationPlan.MemberType,
            cancellationToken);
        var factoryMapNewMemberMappings =
            BuildFactoryMapNewMemberMappings(
                memberMappings,
                templateMapping);
        var factoryMapping = BuildFactoryMapping(
            destinationPlan,
            templateMapping,
            factoryMapNewMemberMappings,
            runtimeLocals,
            mapperType);
        var constructorMapping = BuildConstructorMapping(
            registration.SourceType,
            destinationPlan.MemberType,
            memberMappings,
            templateMapping,
            runtimeLocals,
            compilation,
            mapperType,
            cancellationToken);
        var mapExistingDestinationLocalName =
            destinationPlan.MapExistingKind ==
                TypeMapperMapExistingKind.NullableValue &&
            !memberMappings.MapExisting.IsEmpty
                ? AllocateDestinationValueLocalName(mapperType)
                : null;
        var mapExistingMemberMappings =
            BuildMapExistingMemberMappings(
                memberMappings.MapExisting,
                templateMapping,
                mapperType,
                mapExistingDestinationLocalName);

        var mapping = new TypeMapperMappingModel(
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                registration.SourceType),
            TypeMapperMappingTypePolicy
                .GetGeneratedMaybeNullTypeName(
                    registration.SourceType),
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                registration.DestinationType),
            TypeMapperMappingTypePolicy
                .GetGeneratedMaybeNullTypeName(
                    registration.DestinationType),
            templateMapping?.MapNewDirectExpression,
            templateMapping?.MapExistingDirectExpression,
            factoryMapping,
            constructorMapping?.Constructor,
            destinationPlan.MapExistingKind,
            mapExistingDestinationLocalName,
            factoryMapping is not null
                ? factoryMapNewMemberMappings
                : constructorMapping?.MapNewMemberMappings ??
                  memberMappings.MapNew,
            mapExistingMemberMappings,
            MapNewUnsupportedExceptionMessage:
                templateMapping?
                    .Factory?
                    .UnsupportedMessage);

        return mapping;
    }

    private static PlannedControlFlowNode BuildPlannedControlFlow(
        TemplateMappingPlanNode node,
        Func<TemplateMappingPlan, TypeMapperMappingModel>
            buildLeaf)
    {
        if (node is
            TemplateLocalDeclarationsMappingPlanNode localDeclarations)
        {
            var next = BuildPlannedControlFlow(
                localDeclarations.Next,
                buildLeaf);

            return next with
            {
                RuntimeLocalPlaceholders =
                    localDeclarations.RuntimeLocalPlaceholders
                        .AddRange(
                            next.RuntimeLocalPlaceholders)
            };
        }

        if (node is TemplateLeafMappingPlanNode leaf)
        {
            return new PlannedControlFlowNode(
                RuntimeLocalPlaceholders: [],
                MapNewCondition: null,
                MapExistingCondition: null,
                WhenTrue: null,
                WhenFalse: null,
                buildLeaf(leaf.Plan),
                MapNewThrowExpression: null,
                MapExistingThrowExpression: null);
        }

        if (node is TemplateThrowMappingPlanNode throwNode)
        {
            return new PlannedControlFlowNode(
                RuntimeLocalPlaceholders: [],
                MapNewCondition: null,
                MapExistingCondition: null,
                WhenTrue: null,
                WhenFalse: null,
                Leaf: null,
                throwNode.MapNewExpression,
                throwNode.MapExistingExpression);
        }

        var conditional =
            (TemplateConditionalMappingPlanNode)node;

        return new PlannedControlFlowNode(
            RuntimeLocalPlaceholders: [],
            conditional.MapNewCondition,
            conditional.MapExistingCondition,
            BuildPlannedControlFlow(
                conditional.WhenTrue,
                buildLeaf),
            BuildPlannedControlFlow(
                conditional.WhenFalse,
                buildLeaf),
            Leaf: null,
            MapNewThrowExpression: null,
            MapExistingThrowExpression: null);
    }

    private static TypeMapperControlFlowNode BuildModeControlFlow(
        PlannedControlFlowNode node,
        IReadOnlyDictionary<string, TemplateRuntimeLocalPlan>
            runtimeLocals,
        bool mapNew)
    {
        TypeMapperControlFlowNode result;

        if (node.Leaf is { } leaf)
        {
            result = new TypeMapperControlFlowNode(
                Locals: [],
                Condition: null,
                WhenTrue: null,
                WhenFalse: null,
                leaf,
                ThrowExpression: null);
        }
        else if ((mapNew
                     ? node.MapNewThrowExpression
                     : node.MapExistingThrowExpression) is
                 { } throwExpression)
        {
            result = new TypeMapperControlFlowNode(
                Locals: [],
                Condition: null,
                WhenTrue: null,
                WhenFalse: null,
                Leaf: null,
                throwExpression);
        }
        else
        {
            var whenTrue = BuildModeControlFlow(
                node.WhenTrue!,
                runtimeLocals,
                mapNew);
            var whenFalse = BuildModeControlFlow(
                node.WhenFalse!,
                runtimeLocals,
                mapNew);

            if (AreEquivalentControlFlow(
                    whenTrue,
                    whenFalse,
                    mapNew))
            {
                result = whenTrue;
            }
            else
            {
                var condition =
                    mapNew
                        ? node.MapNewCondition!
                        : node.MapExistingCondition!;

                if (StringComparer.Ordinal.Equals(
                        condition,
                        "true"))
                {
                    result = whenTrue;
                }
                else if (StringComparer.Ordinal.Equals(
                             condition,
                             "false"))
                {
                    result = whenFalse;
                }
                else
                {
                    result = new TypeMapperControlFlowNode(
                        Locals: [],
                        condition,
                        whenTrue,
                        whenFalse,
                        Leaf: null,
                        ThrowExpression: null);
                }
            }
        }

        if (node.RuntimeLocalPlaceholders.IsEmpty)
        {
            return result;
        }

        var locals = node.RuntimeLocalPlaceholders
            .Select(placeholder =>
            {
                var local = runtimeLocals[placeholder];

                return new TypeMapperLocalValueModel(
                    local.DeclarationType,
                    placeholder,
                    mapNew
                        ? local.MapNewExpression
                        : local.MapExistingExpression,
                    local.IsConst);
            })
            .ToImmutableArray();

        return result with
        {
            Locals = locals.AddRange(result.Locals)
        };
    }

    private static bool AreEquivalentControlFlow(
        TypeMapperControlFlowNode left,
        TypeMapperControlFlowNode right,
        bool mapNew)
    {
        if (left.Leaf is { } leftLeaf &&
            right.Leaf is { } rightLeaf)
        {
            return mapNew
                ? AreEquivalentMapNew(
                    leftLeaf,
                    rightLeaf)
                : AreEquivalentMapExisting(
                    leftLeaf,
                    rightLeaf);
        }

        if (left.ThrowExpression is { } leftThrow &&
            right.ThrowExpression is { } rightThrow)
        {
            return StringComparer.Ordinal.Equals(
                leftThrow,
                rightThrow);
        }

        return left.Leaf is null &&
               right.Leaf is null &&
               left.ThrowExpression is null &&
               right.ThrowExpression is null &&
               StringComparer.Ordinal.Equals(
                   left.Condition,
                   right.Condition) &&
               AreEquivalentControlFlow(
                   left.WhenTrue!,
                   right.WhenTrue!,
                   mapNew) &&
               AreEquivalentControlFlow(
                   left.WhenFalse!,
                   right.WhenFalse!,
                   mapNew);
    }

    private static bool AreEquivalentMapNew(
        TypeMapperMappingModel left,
        TypeMapperMappingModel right)
    {
        return StringComparer.Ordinal.Equals(
                   left.MapNewUnsupportedExceptionMessage,
                   right.MapNewUnsupportedExceptionMessage) &&
               StringComparer.Ordinal.Equals(
                   left.MapNewDirectExpression,
                   right.MapNewDirectExpression) &&
               Equals(
                   left.MapNewFactory,
                   right.MapNewFactory) &&
               Equals(
                   left.MapNewConstructor,
                   right.MapNewConstructor) &&
               left.MapNewMemberMappings.SequenceEqual(
                   right.MapNewMemberMappings);
    }

    private static bool AreEquivalentMapExisting(
        TypeMapperMappingModel left,
        TypeMapperMappingModel right)
    {
        return StringComparer.Ordinal.Equals(
                   left.MapExistingUnsupportedExceptionMessage,
                   right.MapExistingUnsupportedExceptionMessage) &&
               StringComparer.Ordinal.Equals(
                   left.MapExistingDirectExpression,
                   right.MapExistingDirectExpression) &&
               left.MapExistingKind ==
               right.MapExistingKind &&
               StringComparer.Ordinal.Equals(
                   left.MapExistingDestinationLocalName,
                   right.MapExistingDestinationLocalName) &&
               left.MapExistingMemberMappings.SequenceEqual(
                   right.MapExistingMemberMappings);
    }

    private static TypeMapperControlFlowNode
        HoistConditionalConstructorValues(
            TypeMapperControlFlowNode node,
            INamedTypeSymbol mapperType)
    {
        if (node.Condition is null)
        {
            return node;
        }

        var whenTrue =
            HoistConditionalConstructorValues(
                node.WhenTrue!,
                mapperType);
        var whenFalse =
            HoistConditionalConstructorValues(
                node.WhenFalse!,
                mapperType);

        if (AreEquivalentControlFlow(
                whenTrue,
                whenFalse,
                mapNew: true))
        {
            return whenTrue with
            {
                Locals = node.Locals.AddRange(
                    whenTrue.Locals)
            };
        }

        if (TryMergeConditionalConstructorValues(
                node.Condition!,
                whenTrue,
                whenFalse,
                mapperType,
                out var merged))
        {
            return new TypeMapperControlFlowNode(
                node.Locals,
                Condition: null,
                WhenTrue: null,
                WhenFalse: null,
                merged,
                ThrowExpression: null);
        }

        return node with
        {
            WhenTrue = whenTrue,
            WhenFalse = whenFalse
        };
    }

    private static bool TryMergeConditionalConstructorValues(
        string condition,
        TypeMapperControlFlowNode whenTrue,
        TypeMapperControlFlowNode whenFalse,
        INamedTypeSymbol mapperType,
        out TypeMapperMappingModel merged)
    {
        merged = default;

        if (!whenTrue.Locals.IsEmpty ||
            !whenFalse.Locals.IsEmpty ||
            whenTrue.Leaf is not { } trueMapping ||
            whenFalse.Leaf is not { } falseMapping ||
            trueMapping.MapNewUnsupportedExceptionMessage is not null ||
            falseMapping.MapNewUnsupportedExceptionMessage is not null ||
            trueMapping.MapNewDirectExpression is not null ||
            falseMapping.MapNewDirectExpression is not null ||
            trueMapping.MapNewFactory is not null ||
            falseMapping.MapNewFactory is not null ||
            trueMapping.MapNewConstructor is not
                { } trueConstructor ||
            falseMapping.MapNewConstructor is not
                { } falseConstructor ||
            !StringComparer.Ordinal.Equals(
                trueConstructor.ConstructedTypeName,
                falseConstructor.ConstructedTypeName) ||
            trueConstructor.Arguments.Length !=
                falseConstructor.Arguments.Length ||
            !trueMapping.MapNewMemberMappings.SequenceEqual(
                falseMapping.MapNewMemberMappings))
        {
            return false;
        }

        var lastDifferentArgumentIndex = -1;

        for (var index = 0;
             index < trueConstructor.Arguments.Length;
             index++)
        {
            var trueArgument =
                trueConstructor.Arguments[index];
            var falseArgument =
                falseConstructor.Arguments[index];

            if (trueArgument.Equals(falseArgument))
            {
                continue;
            }

            if (!CanMergeConditionalConstructorArgument(
                    trueArgument,
                    falseArgument))
            {
                return false;
            }

            lastDifferentArgumentIndex = index;
        }

        if (lastDifferentArgumentIndex < 0)
        {
            return false;
        }

        var usedNames =
            ConventionConstructorMappingPlanner
                .BuildUsedValueLocalNames(mapperType);

        CollectMapNewGeneratedLocalNames(
            trueMapping,
            usedNames);
        CollectMapNewGeneratedLocalNames(
            falseMapping,
            usedNames);

        var arguments =
            ImmutableArray.CreateBuilder<
                TypeMapperConstructorArgumentMappingModel>(
                trueConstructor.Arguments.Length);

        for (var index = 0;
             index < trueConstructor.Arguments.Length;
             index++)
        {
            var trueArgument =
                trueConstructor.Arguments[index];

            if (index > lastDifferentArgumentIndex ||
                trueArgument.Equals(
                    falseConstructor.Arguments[index]) &&
                trueArgument.ValueLocalName is not null)
            {
                arguments.Add(trueArgument);
                continue;
            }

            var falseArgument =
                falseConstructor.Arguments[index];
            var valueLocalName =
                trueArgument.ValueLocalName ??
                AllocateUserLocalName(
                    trueArgument.ParameterName,
                    usedNames);
            var valueExpression =
                trueArgument.Equals(falseArgument)
                    ? GetUncachedConstructorArgumentValue(
                        trueArgument)
                    : BuildConditionalValueExpression(
                        condition,
                        GetUncachedConstructorArgumentValue(
                            trueArgument),
                        GetUncachedConstructorArgumentValue(
                            falseArgument));

            arguments.Add(
                trueArgument with
                {
                    SourceMemberName = string.Empty,
                    ValueLocalName = valueLocalName,
                    ExplicitValueExpression =
                        valueExpression,
                    ValueLocalTypeName =
                        trueArgument.TargetTypeName ??
                        falseArgument.TargetTypeName ??
                        trueArgument.ValueLocalTypeName ??
                        falseArgument.ValueLocalTypeName
                });
        }

        merged = trueMapping with
        {
            MapNewConstructor =
                trueConstructor with
                {
                    Arguments = arguments.ToImmutable()
                }
        };

        return true;
    }

    private static void CollectMapNewGeneratedLocalNames(
        TypeMapperMappingModel mapping,
        HashSet<string> result)
    {
        if (mapping.MapNewFactory is { } factory)
        {
            if (factory.Delegate is { } factoryDelegate)
            {
                AddUsedLocalName(
                    result,
                    factoryDelegate.LocalName);
            }

            AddUsedLocalName(
                result,
                factory.DestinationLocalName);

            if (factory.NullableValueLocalName is
                { } nullableValueLocalName)
            {
                AddUsedLocalName(
                    result,
                    nullableValueLocalName);
            }
        }

        if (mapping.MapNewConstructor is
            { } constructor)
        {
            foreach (var argument in constructor.Arguments)
            {
                if (argument.ValueLocalName is
                    { } valueLocalName)
                {
                    AddUsedLocalName(
                        result,
                        valueLocalName);
                }
            }
        }

        foreach (var memberMapping in
                 mapping.MapNewMemberMappings)
        {
            if (memberMapping.SourceValueLocalName is
                { } sourceValueLocalName)
            {
                AddUsedLocalName(
                    result,
                    sourceValueLocalName);
            }

            if (memberMapping.ValueLocalName is
                { } valueLocalName)
            {
                AddUsedLocalName(
                    result,
                    valueLocalName);
            }
        }
    }

    private static void AddUsedLocalName(
        HashSet<string> result,
        string name)
    {
        result.Add(
            name.Length > 0 && name[0] == '@'
                ? name.Substring(1)
                : name);
    }

    private static bool
        CanMergeConditionalConstructorArgument(
            TypeMapperConstructorArgumentMappingModel whenTrue,
            TypeMapperConstructorArgumentMappingModel whenFalse)
    {
        if (!StringComparer.Ordinal.Equals(
                whenTrue.ParameterName,
                whenFalse.ParameterName) ||
            (whenTrue.ValueLocalName is null) !=
            (whenFalse.ValueLocalName is null) ||
            whenTrue.ValueLocalName is not null &&
            !StringComparer.Ordinal.Equals(
                whenTrue.ValueLocalName,
                whenFalse.ValueLocalName))
        {
            return false;
        }

        var trueTargetType =
            whenTrue.TargetTypeName ??
            whenTrue.ValueLocalTypeName;
        var falseTargetType =
            whenFalse.TargetTypeName ??
            whenFalse.ValueLocalTypeName;

        return trueTargetType is null ||
               falseTargetType is null ||
               StringComparer.Ordinal.Equals(
                   trueTargetType,
                   falseTargetType);
    }

    private static string
        GetUncachedConstructorArgumentValue(
            TypeMapperConstructorArgumentMappingModel argument)
    {
        if (argument.ExplicitValueExpression is
            { } explicitValue)
        {
            return explicitValue;
        }

        if (argument.SourceMemberName.Length == 0)
        {
            throw new InvalidOperationException(
                "Constructor argument requires a value.");
        }

        return
            $"source!.{EscapeIdentifier(argument.SourceMemberName)}";
    }

    private static string BuildConditionalValueExpression(
        string condition,
        string whenTrue,
        string whenFalse)
    {
        var expression = SyntaxFactory.ParseExpression(
            ParenthesizeConditionalExpression(condition) +
            " ? " +
            ParenthesizeConditionalExpression(whenTrue) +
            " : " +
            ParenthesizeConditionalExpression(whenFalse));

        return expression
            .WithoutTrivia()
            .NormalizeWhitespace()
            .ToFullString();
    }

    private static string ParenthesizeConditionalExpression(
        string expression)
    {
        return SyntaxFactory.ParseExpression(expression) is
            ConditionalExpressionSyntax
                ? $"({expression})"
                : expression;
    }

    private static TypeMapperMappingModel CombineModeMappings(
        TypeMapperMappingModel mapNew,
        TypeMapperMappingModel mapExisting)
    {
        return mapNew with
        {
            MapExistingDirectExpression =
                mapExisting.MapExistingDirectExpression,
            MapExistingKind =
                mapExisting.MapExistingKind,
            MapExistingDestinationLocalName =
                mapExisting.MapExistingDestinationLocalName,
            MapExistingMemberMappings =
                mapExisting.MapExistingMemberMappings,
            MapExistingUnsupportedExceptionMessage =
                mapExisting.MapExistingUnsupportedExceptionMessage,
            ControlFlow = null
        };
    }

    private static TypeMapperMappingModel? FindFirstLeaf(
        TypeMapperControlFlowNode node)
    {
        if (node.Leaf is { } leaf)
        {
            return leaf;
        }

        if (node.Condition is null)
        {
            return null;
        }

        return FindFirstLeaf(node.WhenTrue!) ??
               FindFirstLeaf(node.WhenFalse!);
    }

    private static TypeMapperControlFlowMappingModel
        BuildControlFlowModel(
            ImmutableArray<TemplateRuntimeLocalPlan> runtimeLocals,
            TypeMapperControlFlowNode mapNewRoot,
            TypeMapperControlFlowNode mapExistingRoot,
            INamedTypeSymbol mapperType)
    {
        var mapNewRequiredLocals =
            CollectRequiredLocals(
                runtimeLocals,
                mapNewRoot,
                mapNew: true);
        var mapExistingRequiredLocals =
            CollectRequiredLocals(
                runtimeLocals,
                mapExistingRoot,
                mapNew: false);
        var mapNewNames =
            AllocateRuntimeLocalNames(
                runtimeLocals,
                mapNewRequiredLocals,
                mapNewRoot,
                mapperType,
                hasDestinationParameter: false,
                mapNew: true);
        var mapExistingNames =
            AllocateRuntimeLocalNames(
                runtimeLocals,
                mapExistingRequiredLocals,
                mapExistingRoot,
                mapperType,
                hasDestinationParameter: true,
                mapNew: false);
        var renamedMapNewRoot =
            RenameControlFlow(
                mapNewRoot,
                mapNewNames,
                mapNewRequiredLocals);
        var renamedMapExistingRoot =
            RenameControlFlow(
                mapExistingRoot,
                mapExistingNames,
                mapExistingRequiredLocals);

        return new TypeMapperControlFlowMappingModel(
            renamedMapNewRoot,
            renamedMapExistingRoot);
    }

    private static IReadOnlyDictionary<string, string>
        AllocateRuntimeLocalNames(
            ImmutableArray<TemplateRuntimeLocalPlan> runtimeLocals,
            HashSet<string> requiredLocals,
            TypeMapperControlFlowNode root,
            INamedTypeSymbol mapperType,
            bool hasDestinationParameter,
            bool mapNew)
    {
        var reservedNames =
            ConventionConstructorMappingPlanner
                .BuildUsedValueLocalNames(mapperType);

        if (hasDestinationParameter)
        {
            reservedNames.Add("destination");
        }

        var generatedNamesByPlaceholder =
            new Dictionary<string, HashSet<string>>(
                StringComparer.Ordinal);

        CollectRuntimeLocalGeneratedNameConstraints(
            root,
            requiredLocals,
            mapNew,
            generatedNamesByPlaceholder);
        var preferredNames =
            new HashSet<string>(
                runtimeLocals
                    .Where(local =>
                        requiredLocals.Contains(
                            local.PlaceholderName))
                    .Select(static local =>
                        local.PreferredName),
                StringComparer.Ordinal);
        var allocatedNames =
            new Dictionary<string, string>(
                StringComparer.Ordinal);
        var allocatedFinalNamesByPreferredName =
            new Dictionary<string, HashSet<string>>(
                StringComparer.Ordinal);

        foreach (var local in runtimeLocals)
        {
            if (!requiredLocals.Contains(
                    local.PlaceholderName))
            {
                continue;
            }

            var usedNames =
                new HashSet<string>(
                    reservedNames,
                    StringComparer.Ordinal);

            foreach (var preferredName in
                     preferredNames)
            {
                if (!StringComparer.Ordinal.Equals(
                        preferredName,
                        local.PreferredName))
                {
                    usedNames.Add(preferredName);
                }
            }

            foreach (var allocatedGroup in
                     allocatedFinalNamesByPreferredName)
            {
                if (!StringComparer.Ordinal.Equals(
                        allocatedGroup.Key,
                        local.PreferredName))
                {
                    usedNames.UnionWith(
                        allocatedGroup.Value);
                }
            }

            if (generatedNamesByPlaceholder.TryGetValue(
                    local.PlaceholderName,
                    out var generatedNames))
            {
                usedNames.UnionWith(generatedNames);
            }

            var allocatedName = AllocateUserLocalName(
                local.PreferredName,
                usedNames);

            if (!allocatedFinalNamesByPreferredName.TryGetValue(
                    local.PreferredName,
                    out var allocatedGroupNames))
            {
                allocatedGroupNames =
                    new HashSet<string>(StringComparer.Ordinal);
                allocatedFinalNamesByPreferredName.Add(
                    local.PreferredName,
                    allocatedGroupNames);
            }

            allocatedGroupNames.Add(
                allocatedName.StartsWith(
                    "@",
                    StringComparison.Ordinal)
                    ? allocatedName.Substring(1)
                    : allocatedName);
            allocatedNames.Add(
                local.PlaceholderName,
                allocatedName);
        }

        return allocatedNames;
    }

    private static void
        CollectRuntimeLocalGeneratedNameConstraints(
            TypeMapperControlFlowNode node,
            HashSet<string> requiredLocals,
            bool mapNew,
            Dictionary<string, HashSet<string>>
                generatedNamesByPlaceholder)
    {
        foreach (var local in node.Locals)
        {
            if (!requiredLocals.Contains(local.Name))
            {
                continue;
            }

            if (!generatedNamesByPlaceholder.TryGetValue(
                    local.Name,
                    out var generatedNames))
            {
                generatedNames =
                    new HashSet<string>(StringComparer.Ordinal);
                generatedNamesByPlaceholder.Add(
                    local.Name,
                    generatedNames);
            }

            CollectGeneratedLocalNames(
                node,
                generatedNames,
                mapNew);
        }

        if (node.Condition is null)
        {
            return;
        }

        CollectRuntimeLocalGeneratedNameConstraints(
            node.WhenTrue!,
            requiredLocals,
            mapNew,
            generatedNamesByPlaceholder);
        CollectRuntimeLocalGeneratedNameConstraints(
            node.WhenFalse!,
            requiredLocals,
            mapNew,
            generatedNamesByPlaceholder);
    }

    private static HashSet<string> CollectRequiredLocals(
        ImmutableArray<TemplateRuntimeLocalPlan> runtimeLocals,
        TypeMapperControlFlowNode root,
        bool mapNew)
    {
        var result =
            new HashSet<string>(StringComparer.Ordinal);
        var expressions =
            EnumerateControlFlowExpressions(
                    root,
                    mapNew)
                .ToArray();

        foreach (var local in runtimeLocals)
        {
            if (expressions.Any(expression =>
                    ReferencesIdentifier(
                        expression,
                        local.PlaceholderName)))
            {
                result.Add(local.PlaceholderName);
            }
        }

        var changed = true;

        while (changed)
        {
            changed = false;

            foreach (var local in runtimeLocals)
            {
                if (!result.Contains(
                        local.PlaceholderName))
                {
                    continue;
                }

                var initializer =
                    mapNew
                        ? local.MapNewExpression
                        : local.MapExistingExpression;

                foreach (var dependency in runtimeLocals)
                {
                    if (!result.Contains(
                            dependency.PlaceholderName) &&
                        ReferencesIdentifier(
                            initializer,
                            dependency.PlaceholderName))
                    {
                        result.Add(
                            dependency.PlaceholderName);
                        changed = true;
                    }
                }
            }
        }

        return result;
    }

    private static IEnumerable<string>
        EnumerateControlFlowExpressions(
            TypeMapperControlFlowNode node,
            bool mapNew)
    {
        if (node.Condition is { } condition)
        {
            yield return condition;

            foreach (var expression in
                     EnumerateControlFlowExpressions(
                         node.WhenTrue!,
                         mapNew))
            {
                yield return expression;
            }

            foreach (var expression in
                     EnumerateControlFlowExpressions(
                         node.WhenFalse!,
                         mapNew))
            {
                yield return expression;
            }

            yield break;
        }

        if (node.ThrowExpression is { } throwExpression)
        {
            yield return throwExpression;
            yield break;
        }

        var leaf = node.Leaf!.Value;

        if (!mapNew &&
            leaf.MapExistingUnsupportedExceptionMessage is not null)
        {
            yield break;
        }

        var directExpression =
            mapNew
                ? leaf.MapNewDirectExpression
                : leaf.MapExistingDirectExpression;

        if (mapNew &&
            leaf.MapNewUnsupportedExceptionMessage is not null)
        {
            yield break;
        }

        if (directExpression is not null)
        {
            yield return directExpression;
        }

        if (mapNew)
        {
            if (leaf.MapNewFactory is
                { } factory)
            {
                foreach (var dependency in
                         factory.RuntimeLocalDependencies)
                {
                    yield return dependency;
                }

                yield return factory.Delegate is
                    { } factoryDelegate
                        ? factoryDelegate.ValueExpression
                        : factory.ValueExpression;
            }

            if (leaf.MapNewConstructor is
                { } constructor)
            {
                foreach (var argument in
                         constructor.Arguments)
                {
                    if (argument.ExplicitValueExpression is
                        { } argumentExpression)
                    {
                        yield return argumentExpression;
                    }
                }
            }

            foreach (var mapping in
                     leaf.MapNewMemberMappings)
            {
                if (mapping.ExplicitValueExpression is
                    { } memberExpression)
                {
                    yield return memberExpression;
                }
            }

            yield break;
        }

        foreach (var mapping in
                 leaf.MapExistingMemberMappings)
        {
            if (mapping.ExplicitValueExpression is
                { } memberExpression)
            {
                yield return memberExpression;
            }
        }
    }

    private static bool ReferencesIdentifier(
        string expression,
        string identifier)
    {
        return SyntaxFactory.ParseExpression(expression)
            .DescendantTokens()
            .Any(token =>
                token.IsKind(
                    SyntaxKind.IdentifierToken) &&
                StringComparer.Ordinal.Equals(
                    token.ValueText,
                    identifier));
    }

    private static void CollectGeneratedLocalNames(
        TypeMapperControlFlowNode node,
        HashSet<string> result,
        bool mapNew)
    {
        if (node.Condition is not null)
        {
            CollectGeneratedLocalNames(
                node.WhenTrue!,
                result,
                mapNew);
            CollectGeneratedLocalNames(
                node.WhenFalse!,
                result,
                mapNew);
            return;
        }

        if (node.ThrowExpression is not null)
        {
            return;
        }

        var leaf = node.Leaf!.Value;

        if (mapNew &&
            leaf.MapNewFactory is { } factory)
        {
            if (factory.LocalFunctionName is
                { } localFunctionName)
            {
                AddUsedLocalName(
                    result,
                    localFunctionName);
            }

            if (factory.Delegate is { } factoryDelegate)
            {
                AddUsedLocalName(
                    result,
                    factoryDelegate.LocalName);
            }

            AddUsedLocalName(
                result,
                factory.DestinationLocalName);

            if (factory.NullableValueLocalName is
                { } nullableValueLocalName)
            {
                AddUsedLocalName(
                    result,
                    nullableValueLocalName);
            }
        }

        if (!mapNew &&
            leaf.MapExistingDestinationLocalName is
            { } destinationLocalName)
        {
            AddUsedLocalName(
                result,
                destinationLocalName);
        }

        if (mapNew &&
            leaf.MapNewConstructor is { } constructor)
        {
            foreach (var argument in constructor.Arguments)
            {
                if (argument.ValueLocalName is
                    { } valueLocalName)
                {
                    AddUsedLocalName(
                        result,
                        valueLocalName);
                }
            }
        }

        var memberMappings =
            mapNew
                ? leaf.MapNewMemberMappings
                : leaf.MapExistingMemberMappings;

        foreach (var mapping in memberMappings)
        {
            if (mapping.SourceValueLocalName is
                { } sourceValueLocalName)
            {
                AddUsedLocalName(
                    result,
                    sourceValueLocalName);
            }

            if (mapping.ValueLocalName is
                { } valueLocalName)
            {
                AddUsedLocalName(
                    result,
                    valueLocalName);
            }
        }
    }

    private static string AllocateUserLocalName(
        string preferredName,
        HashSet<string> usedNames)
    {
        if (usedNames.Add(preferredName))
        {
            return EscapeIdentifier(preferredName);
        }

        for (var suffix = 1;; suffix++)
        {
            var candidate =
                preferredName +
                suffix.ToString(
                    CultureInfo.InvariantCulture);

            if (usedNames.Add(candidate))
            {
                return EscapeIdentifier(candidate);
            }
        }
    }

    private static TypeMapperControlFlowNode
        RenameControlFlow(
            TypeMapperControlFlowNode node,
            IReadOnlyDictionary<string, string> names,
            HashSet<string> requiredLocals)
    {
        var locals = node.Locals
            .Where(local =>
                requiredLocals.Contains(local.Name))
            .Select(local =>
                local with
                {
                    Name = names[local.Name],
                    ValueExpression = RenameExpression(
                        local.ValueExpression,
                        names)
                })
            .ToImmutableArray();

        if (node.Condition is { } condition)
        {
            return node with
            {
                Locals = locals,
                Condition = RenameExpression(
                    condition,
                    names),
                WhenTrue = RenameControlFlow(
                    node.WhenTrue!,
                    names,
                    requiredLocals),
                WhenFalse = RenameControlFlow(
                    node.WhenFalse!,
                    names,
                    requiredLocals)
            };
        }

        if (node.ThrowExpression is { } throwExpression)
        {
            return node with
            {
                Locals = locals,
                ThrowExpression = RenameExpression(
                    throwExpression,
                    names)
            };
        }

        return node with
        {
            Locals = locals,
            Leaf = RenameMappingExpressions(
                node.Leaf!.Value,
                names)
        };
    }

    private static TypeMapperMappingModel
        RenameMappingExpressions(
            TypeMapperMappingModel mapping,
            IReadOnlyDictionary<string, string> names)
    {
        return mapping with
        {
            MapNewDirectExpression =
                RenameNullableExpression(
                    mapping.MapNewDirectExpression,
                    names),
            MapExistingDirectExpression =
                RenameNullableExpression(
                    mapping.MapExistingDirectExpression,
                    names),
            MapNewFactory =
                mapping.MapNewFactory is { } factory
                    ? factory with
                    {
                        LocalFunctionDeclaration =
                            factory.LocalFunctionDeclaration is
                                { } localFunctionDeclaration
                                ? RenameLocalFunctionDeclaration(
                                    localFunctionDeclaration,
                                    names)
                                : null,
                        ValueExpression =
                            RenameExpression(
                                factory.ValueExpression,
                                names),
                        Delegate =
                            factory.Delegate is
                                { } factoryDelegate
                                ? factoryDelegate with
                                {
                                    ValueExpression =
                                        RenameExpression(
                                            factoryDelegate
                                                .ValueExpression,
                                            names)
                                }
                                : null,
                        RuntimeLocalDependencies =
                            factory
                                .RuntimeLocalDependencies
                                .Select(dependency =>
                                    RenameExpression(
                                        dependency,
                                        names))
                                .ToImmutableArray()
                    }
                    : null,
            MapNewConstructor =
                mapping.MapNewConstructor is
                    { } constructor
                    ? constructor with
                    {
                        Arguments = constructor.Arguments
                            .Select(argument =>
                                argument with
                                {
                                    ExplicitValueExpression =
                                        RenameNullableExpression(
                                            argument
                                                .ExplicitValueExpression,
                                            names)
                                })
                            .ToImmutableArray()
                    }
                    : null,
            MapNewMemberMappings =
                RenameMemberMappingExpressions(
                    mapping.MapNewMemberMappings,
                    names),
            MapExistingMemberMappings =
                RenameMemberMappingExpressions(
                    mapping.MapExistingMemberMappings,
                    names)
        };
    }

    private static ImmutableArray<
            TypeMapperMemberMappingModel>
        RenameMemberMappingExpressions(
            ImmutableArray<TypeMapperMemberMappingModel> mappings,
            IReadOnlyDictionary<string, string> names)
    {
        return mappings
            .Select(mapping =>
                mapping with
                {
                    ExplicitValueExpression =
                        RenameNullableExpression(
                            mapping.ExplicitValueExpression,
                            names)
                })
            .ToImmutableArray();
    }

    private static string? RenameNullableExpression(
        string? expression,
        IReadOnlyDictionary<string, string> names)
    {
        return expression is null
            ? null
            : RenameExpression(
                expression,
                names);
    }

    private static string RenameExpression(
        string expression,
        IReadOnlyDictionary<string, string> names)
    {
        return new PlaceholderExpressionRewriter(
                    names)
            .Visit(
                SyntaxFactory.ParseExpression(
                    expression))!
            .ToFullString();
    }

    private static string RenameLocalFunctionDeclaration(
        string declaration,
        IReadOnlyDictionary<string, string> names)
    {
        return new PlaceholderExpressionRewriter(
                    names)
            .Visit(
                SyntaxFactory.ParseStatement(
                    declaration))!
            .ToFullString();
    }

    private static ImmutableArray<TypeMapperMemberMappingModel>
        BuildFactoryMapNewMemberMappings(
            ConventionMemberMappingPlan memberMappings,
            TemplateMappingPlan? template)
    {
        if (template is not
            {
                ConstructionKind:
                    TemplateConstructionKind.ByFactory
            })
        {
            return [];
        }

        var assignableMemberNames =
            new HashSet<string>(
                memberMappings.MapExisting.Select(
                    static mapping =>
                        mapping.DestinationMemberName),
                StringComparer.Ordinal);

        return memberMappings.MapNew
            .Where(mapping =>
                assignableMemberNames.Contains(
                    mapping.DestinationMemberName))
            .ToImmutableArray();
    }

    private static ImmutableArray<TypeMapperMemberMappingModel>
        BuildMapExistingMemberMappings(
            ImmutableArray<TypeMapperMemberMappingModel> mappings,
            TemplateMappingPlan? template,
            INamedTypeSymbol mapperType,
            string? destinationLocalName)
    {
        if (template is not
            {
                HasDestinationParameter: true,
                MapExistingDirectExpression: null
            })
        {
            return mappings;
        }

        var result = mappings.ToArray();
        var usedNames =
            ConventionConstructorMappingPlanner
                .BuildUsedValueLocalNames(mapperType);

        usedNames.Add("destination");

        if (destinationLocalName is not null)
        {
            usedNames.Add(destinationLocalName);
        }

        for (var index = 0; index < result.Length; index++)
        {
            var mapping = result[index];

            if (mapping.ExplicitValueExpression is null ||
                !mapping.RequiresPreviousDestinationValueLocal)
            {
                continue;
            }

            if (mapping.ExplicitValueTypeName is null)
            {
                throw new InvalidOperationException(
                    "Explicit member mapping requires a value type.");
            }

            result[index] = mapping with
            {
                ValueLocalName =
                    MakeUniquePreviousDestinationValueLocalName(
                        mapping.DestinationMemberName,
                        usedNames)
            };
        }

        return result.ToImmutableArray();
    }

    private static string
        MakeUniquePreviousDestinationValueLocalName(
            string memberName,
            HashSet<string> usedNames)
    {
        var candidate =
            char.ToLowerInvariant(memberName[0]) +
            memberName.Substring(1);

        if (usedNames.Add(candidate))
        {
            return EscapeIdentifier(candidate);
        }

        for (var suffix = 1;; suffix++)
        {
            var name =
                candidate +
                suffix.ToString(CultureInfo.InvariantCulture);

            if (usedNames.Add(name))
            {
                return EscapeIdentifier(name);
            }
        }
    }

    private static string EscapeIdentifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) !=
               SyntaxKind.None
            ? "@" + value
            : value;
    }

    private static TypeMapperFactoryMappingModel?
        BuildFactoryMapping(
            DestinationPlan destinationPlan,
            TemplateMappingPlan? template,
            ImmutableArray<TypeMapperMemberMappingModel>
                mapNewMemberMappings,
            ImmutableArray<TemplateRuntimeLocalPlan>
                templateRuntimeLocals,
            INamedTypeSymbol mapperType)
    {
        if (template is not
            {
                ConstructionKind:
                    TemplateConstructionKind.ByFactory,
                Factory:
                {
                    ValueExpression:
                        { } factoryValueExpression,
                    UnsupportedMessage: null
                } factory
            })
        {
            return null;
        }

        var usedNames =
            ConventionConstructorMappingPlanner
                .BuildUsedValueLocalNames(mapperType);

        foreach (var runtimeLocal in templateRuntimeLocals)
        {
            usedNames.Add(runtimeLocal.PreferredName);
        }

        if (factory.LocalFunctionDeclaration is
            { } localFunctionDeclaration)
        {
            foreach (var token in SyntaxFactory
                         .ParseStatement(
                             localFunctionDeclaration)
                         .DescendantTokens())
            {
                if (token.IsKind(
                        SyntaxKind.IdentifierToken))
                {
                    usedNames.Add(token.ValueText);
                }
            }
        }

        var localNames =
            new Dictionary<string, string>(
                StringComparer.Ordinal);
        string? functionName = null;
        string? functionDeclaration = null;

        if (factory.LocalFunctionPlaceholderName is
                { } functionPlaceholder &&
            factory.LocalFunctionDeclaration is
                { } localFunction)
        {
            foreach (var capture in factory.Captures)
            {
                var name = AllocateUserLocalName(
                    capture.PreferredName,
                    usedNames);

                localNames.Add(
                    capture.PlaceholderName,
                    name);
            }

            functionName = AllocateUserLocalName(
                "CreateByFactory",
                usedNames);

            localNames.Add(
                functionPlaceholder,
                functionName);
            functionDeclaration =
                RenameLocalFunctionDeclaration(
                    localFunction,
                    localNames);
        }

        var valueExpression =
            RenameExpression(
                factoryValueExpression,
                localNames);
        TypeMapperFactoryDelegateModel? factoryDelegate = null;

        if (factory.DelegateTypeName is
            { } factoryDelegateTypeName)
        {
            foreach (var token in SyntaxFactory
                         .ParseExpression(valueExpression)
                         .DescendantTokens())
            {
                if (token.IsKind(
                        SyntaxKind.IdentifierToken))
                {
                    usedNames.Add(token.ValueText);
                }
            }

            var factoryLocalName =
                AllocateUserLocalName(
                    "factory",
                    usedNames);
            factoryDelegate =
                new TypeMapperFactoryDelegateModel(
                    factoryDelegateTypeName,
                    factoryLocalName,
                    valueExpression);
            valueExpression =
                factoryLocalName + "()";
        }

        var destinationLocalName =
            AllocateUserLocalName(
                "destination",
                usedNames);
        var nullableValueLocalName =
            destinationPlan.MapExistingKind ==
                TypeMapperMapExistingKind.NullableValue &&
            !mapNewMemberMappings.IsEmpty
                ? AllocateUserLocalName(
                    "destinationValue",
                    usedNames)
                : null;

        return new TypeMapperFactoryMappingModel(
            functionName,
            functionDeclaration,
            valueExpression,
            factoryDelegate,
            factory.RuntimeLocalDependencies,
            destinationLocalName,
            nullableValueLocalName);
    }

    private static ConventionConstructorMappingPlan?
        BuildConstructorMapping(
            ITypeSymbol source,
            ITypeSymbol? destination,
            ConventionMemberMappingPlan memberMappings,
            TemplateMappingPlan? template,
            ImmutableArray<TemplateRuntimeLocalPlan> runtimeLocals,
            CSharpCompilation compilation,
            INamedTypeSymbol mapperType,
            CancellationToken cancellationToken)
    {
        if (template is not { } templateValue ||
            templateValue.ConstructionKind ==
                TemplateConstructionKind.None)
        {
            return ConventionConstructorMappingPlanner.Build(
                source,
                destination,
                memberMappings,
                compilation,
                mapperType,
                cancellationToken);
        }

        if (templateValue.ConstructionKind ==
            TemplateConstructionKind.TypeParameterParameterless)
        {
            return BuildTypeParameterConstructorMapping(
                destination,
                memberMappings);
        }

        if (templateValue.ConstructionKind ==
            TemplateConstructionKind.ByConvention)
        {
            return templateValue.ConventionConstructorMappings
                    .IsDefault
                ? null
                : ConventionConstructorMappingPlanner.Build(
                    source,
                    destination,
                    memberMappings,
                    templateValue.ConventionConstructorMappings,
                    runtimeLocals,
                    compilation,
                    mapperType,
                    cancellationToken);
        }

        if (templateValue.ConstructionKind ==
            TemplateConstructionKind.ByFactory)
        {
            return null;
        }

        return templateValue.Constructor is { } constructor
            ? BuildTemplateConstructorMapping(
                destination,
                memberMappings,
                constructor,
                mapperType)
            : null;
    }

    private static ConventionMemberMappingPlan MergeMemberMappings(
        ConventionMemberMappingPlan convention,
        TemplateMappingPlan? template,
        ITypeSymbol? destination,
        CancellationToken cancellationToken)
    {
        if (template is not { } value ||
            value.MapNewDirectExpression is not null)
        {
            return convention;
        }

        var templateMemberNames =
            new HashSet<string>(StringComparer.Ordinal);
        var conventionMapNewByName =
            convention.MapNew.ToDictionary(
                static mapping =>
                    mapping.DestinationMemberName,
                StringComparer.Ordinal);
        var conventionMapExistingByName =
            convention.MapExisting.ToDictionary(
                static mapping =>
                    mapping.DestinationMemberName,
                StringComparer.Ordinal);
        var mapNew =
            ImmutableArray.CreateBuilder<
                TypeMapperMemberMappingModel>();
        var mapExisting =
            ImmutableArray.CreateBuilder<
                TypeMapperMemberMappingModel>();

        foreach (var templateMember in value.MemberMappings)
        {
            templateMemberNames.Add(templateMember.MemberName);

            if (templateMember.MapNewMapping is { } explicitMapNew)
            {
                mapNew.Add(explicitMapNew);
            }
            else if (templateMember.Kind ==
                         TemplateMemberMappingKind.Auto &&
                     conventionMapNewByName.TryGetValue(
                         templateMember.MemberName,
                         out var automaticMapNew))
            {
                mapNew.Add(automaticMapNew);
            }

            if (templateMember.MapExistingMapping is
                { } explicitMapExisting)
            {
                mapExisting.Add(explicitMapExisting);
            }
            else if (templateMember.Kind ==
                         TemplateMemberMappingKind.Auto &&
                     conventionMapExistingByName.TryGetValue(
                         templateMember.MemberName,
                         out var automaticMapExisting))
            {
                mapExisting.Add(automaticMapExisting);
            }
        }

        mapNew.AddRange(
            convention.MapNew.Where(mapping =>
                !templateMemberNames.Contains(
                    mapping.DestinationMemberName)));
        mapExisting.AddRange(
            convention.MapExisting.Where(mapping =>
                !templateMemberNames.Contains(
                    mapping.DestinationMemberName)));

        var mapNewMappings = mapNew.ToImmutable();

        return new ConventionMemberMappingPlan(
            mapNewMappings,
            mapExisting.ToImmutable(),
            TemplateMappingPlanner.HasUnmappedRequiredMembers(
                destination,
                mapNewMappings,
                cancellationToken));
    }

    private static ConventionConstructorMappingPlan?
        BuildTypeParameterConstructorMapping(
            ITypeSymbol? destination,
            ConventionMemberMappingPlan memberMappings)
    {
        if (destination is not ITypeParameterSymbol typeParameter ||
            memberMappings.HasUnmappedRequiredMembers ||
            !typeParameter.HasValueTypeConstraint &&
            !typeParameter.HasUnmanagedTypeConstraint &&
            !typeParameter.HasConstructorConstraint)
        {
            return null;
        }

        return new ConventionConstructorMappingPlan(
            new TypeMapperConstructorMappingModel(
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    destination),
                []),
            memberMappings.MapNew);
    }

    private static ConventionConstructorMappingPlan?
        BuildTemplateConstructorMapping(
            ITypeSymbol? destination,
            ConventionMemberMappingPlan memberMappings,
            TemplateConstructorMappingPlan templateConstructor,
            INamedTypeSymbol mapperType)
    {
        var setsRequiredMembers =
            ConventionConstructorMappingPlanner
                .HasSetsRequiredMembersAttribute(
                    templateConstructor.Constructor);

        if (destination is null ||
            memberMappings.HasUnmappedRequiredMembers &&
            !setsRequiredMembers)
        {
            return null;
        }

        var correspondingMemberIndexes =
            new HashSet<int>();

        foreach (var parameter in
                 templateConstructor.Constructor.Parameters)
        {
            if (templateConstructor.IgnoredParameterNames.Contains(
                    parameter.Name,
                    StringComparer.Ordinal))
            {
                continue;
            }

            if (FindCorrespondingMemberIndex(
                    memberMappings.MapNew,
                    parameter.Name) is { } memberIndex)
            {
                correspondingMemberIndexes.Add(memberIndex);
            }
        }

        var correspondingArgumentIndexes =
            new List<int>[memberMappings.MapNew.Length];

        for (var argumentIndex = 0;
             argumentIndex < templateConstructor.Arguments.Length;
             argumentIndex++)
        {
            if (FindCorrespondingMemberIndex(
                    memberMappings.MapNew,
                    templateConstructor.Arguments[argumentIndex]
                        .ParameterName) is not { } memberIndex)
            {
                continue;
            }

            correspondingArgumentIndexes[memberIndex] ??=
                new List<int>();
            correspondingArgumentIndexes[memberIndex]!
                .Add(argumentIndex);
        }

        var mapNew =
            ImmutableArray.CreateBuilder<
                TypeMapperMemberMappingModel>();
        var sharedValues =
            new List<(int MemberIndex, int ArgumentIndex)>();

        for (var index = 0;
             index < memberMappings.MapNew.Length;
             index++)
        {
            var mapping = memberMappings.MapNew[index];

            if (!correspondingMemberIndexes.Contains(index) ||
                mapping.ExplicitValueExpression is not null ||
                mapping.IsRequired && !setsRequiredMembers)
            {
                if (correspondingMemberIndexes.Contains(index) &&
                    mapping.ExplicitValueExpression is null &&
                    mapping.IsRequired &&
                    !setsRequiredMembers &&
                    correspondingArgumentIndexes[index] is
                        { Count: 1 } argumentIndexes)
                {
                    var argumentIndex = argumentIndexes[0];
                    var argument =
                        templateConstructor.Arguments[
                            argumentIndex];

                    if (argument.ExplicitValueExpression is null &&
                        StringComparer.Ordinal.Equals(
                            argument.SourceMemberName,
                            mapping.SourceMemberName))
                    {
                        sharedValues.Add(
                            (mapNew.Count, argumentIndex));
                    }
                }

                mapNew.Add(mapping);
            }
        }

        var argumentModels =
            templateConstructor.Arguments.ToArray();

        if (sharedValues.Count > 0)
        {
            var lastSharedArgumentIndex =
                sharedValues.Max(
                    static value =>
                        value.ArgumentIndex);
            var usedValueLocalNames =
                ConventionConstructorMappingPlanner
                    .BuildUsedValueLocalNames(mapperType);

            for (var argumentIndex = 0;
                 argumentIndex <= lastSharedArgumentIndex;
                 argumentIndex++)
            {
                var argument = argumentModels[argumentIndex];

                argumentModels[argumentIndex] =
                    argument with
                    {
                        ValueLocalName =
                            argument.ExplicitValueExpression is not null
                                ? ConventionConstructorMappingPlanner
                                    .MakeUniqueValueLocalName(
                                        "template",
                                        argument.ParameterName,
                                        usedValueLocalNames)
                                : ConventionConstructorMappingPlanner
                                    .MakeUniqueSourceValueLocalName(
                                        argument.SourceMemberName,
                                        usedValueLocalNames)
                    };
            }

            foreach (var sharedValue in sharedValues)
            {
                var memberMapping =
                    mapNew[sharedValue.MemberIndex];

                mapNew[sharedValue.MemberIndex] =
                    memberMapping with
                    {
                        SourceValueLocalName =
                            argumentModels[
                                sharedValue.ArgumentIndex]
                                .ValueLocalName
                    };
            }
        }

        return new ConventionConstructorMappingPlan(
            new TypeMapperConstructorMappingModel(
                TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                    destination),
                argumentModels.ToImmutableArray()),
            mapNew.ToImmutable());
    }

    private static int? FindCorrespondingMemberIndex(
        ImmutableArray<TypeMapperMemberMappingModel> memberMappings,
        string parameterName)
    {
        for (var index = 0;
             index < memberMappings.Length;
             index++)
        {
            if (StringComparer.Ordinal.Equals(
                    memberMappings[index].DestinationMemberName,
                    parameterName))
            {
                return index;
            }
        }

        int? result = null;

        for (var index = 0;
             index < memberMappings.Length;
             index++)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(
                    memberMappings[index].DestinationMemberName,
                    parameterName))
            {
                continue;
            }

            if (result is not null)
            {
                return null;
            }

            result = index;
        }

        return result;
    }

    private static DestinationPlan BuildDestinationPlan(
        ITypeSymbol destinationType,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var memberType = destinationType;
        var isNullableValue = false;

        if (destinationType is INamedTypeSymbol namedDestination)
        {
            if (DirectDestinationTypePolicy.IsDirect(
                    namedDestination))
            {
                return default;
            }

            if (namedDestination.OriginalDefinition.SpecialType ==
                    SpecialType.System_Nullable_T)
            {
                memberType = namedDestination.TypeArguments[0];
                isNullableValue = true;
            }
        }

        memberType = memberType.WithNullableAnnotation(
            NullableAnnotation.NotAnnotated);

        if (memberType is ITypeParameterSymbol typeParameter)
        {
            return new DestinationPlan(
                memberType,
                isNullableValue
                    ? TypeMapperMapExistingKind.NullableValue
                    : GetTypeParameterMapExistingKind(
                        typeParameter,
                        cancellationToken));
        }

        if (memberType is not INamedTypeSymbol namedMemberType ||
            namedMemberType.IsRefLikeType)
        {
            return default;
        }

        return namedMemberType.TypeKind switch
        {
            TypeKind.Class or TypeKind.Interface =>
                new DestinationPlan(
                    memberType,
                    TypeMapperMapExistingKind.Reference),
            TypeKind.Struct =>
                new DestinationPlan(
                    memberType,
                    isNullableValue
                        ? TypeMapperMapExistingKind.NullableValue
                        : TypeMapperMapExistingKind.Value),
            _ => default
        };
    }

    private static TypeMapperMapExistingKind
        GetTypeParameterMapExistingKind(
            ITypeParameterSymbol typeParameter,
            CancellationToken cancellationToken)
    {
        if (!HasMapExistingConstraint(
                typeParameter,
                new HashSet<ISymbol>(
                    SymbolEqualityComparer.Default),
                cancellationToken))
        {
            return TypeMapperMapExistingKind.Unsupported;
        }

        return typeParameter.HasValueTypeConstraint
            ? TypeMapperMapExistingKind.Value
            : TypeMapperMapExistingKind.Reference;
    }

    private static bool HasMapExistingConstraint(
        ITypeParameterSymbol typeParameter,
        HashSet<ISymbol> visitedTypeParameters,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!visitedTypeParameters.Add(typeParameter))
        {
            return false;
        }

        if (typeParameter.HasReferenceTypeConstraint)
        {
            return true;
        }

        foreach (var constraint in typeParameter.ConstraintTypes)
        {
            if (constraint is ITypeParameterSymbol
                    nestedTypeParameter)
            {
                if (HasMapExistingConstraint(
                        nestedTypeParameter,
                        visitedTypeParameters,
                        cancellationToken))
                {
                    return true;
                }

                continue;
            }

            if (constraint.TypeKind is
                TypeKind.Class or
                TypeKind.Interface)
            {
                return true;
            }
        }

        return false;
    }

    private static string AllocateDestinationValueLocalName(
        INamedTypeSymbol mapperType)
    {
        var usedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "source",
            "destination",
            "context"
        };

        for (var type = mapperType;
             type is not null;
             type = type.ContainingType)
        {
            foreach (var typeParameter in type.TypeParameters)
            {
                usedNames.Add(typeParameter.Name);
            }
        }

        const string candidate = "destinationValue";

        if (usedNames.Add(candidate))
        {
            return candidate;
        }

        for (var suffix = 1;; suffix++)
        {
            var name =
                candidate +
                suffix.ToString(CultureInfo.InvariantCulture);

            if (usedNames.Add(name))
            {
                return name;
            }
        }
    }

    private static string GetAccessibility(
        Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Private => "private",
            Accessibility.Protected => "protected",
            Accessibility.ProtectedAndInternal =>
                "private protected",
            Accessibility.ProtectedOrInternal =>
                "protected internal",
            _ => throw new InvalidOperationException(
                $"Unsupported mapper accessibility: {accessibility}.")
        };
    }

    private readonly record struct TypeMapperGenerationInput(
        string StableIdentity,
        TypeMapperModel Model);

    private sealed record PlannedControlFlowNode(
        ImmutableArray<string> RuntimeLocalPlaceholders,
        string? MapNewCondition,
        string? MapExistingCondition,
        PlannedControlFlowNode? WhenTrue,
        PlannedControlFlowNode? WhenFalse,
        TypeMapperMappingModel? Leaf,
        string? MapNewThrowExpression,
        string? MapExistingThrowExpression);

    private sealed class PlaceholderExpressionRewriter(
        IReadOnlyDictionary<string, string> names)
        : CSharpSyntaxRewriter
    {
        public override SyntaxNode? VisitParameter(
            ParameterSyntax node)
        {
            var rewritten =
                (ParameterSyntax)base.VisitParameter(node)!;

            return names.TryGetValue(
                    node.Identifier.ValueText,
                    out var name)
                ? rewritten.WithIdentifier(
                    SyntaxFactory.Identifier(name)
                        .WithTriviaFrom(
                            rewritten.Identifier))
                : rewritten;
        }

        public override SyntaxNode? VisitLocalFunctionStatement(
            LocalFunctionStatementSyntax node)
        {
            var rewritten =
                (LocalFunctionStatementSyntax)
                base.VisitLocalFunctionStatement(node)!;

            return names.TryGetValue(
                    node.Identifier.ValueText,
                    out var name)
                ? rewritten.WithIdentifier(
                    SyntaxFactory.Identifier(name)
                        .WithTriviaFrom(
                            rewritten.Identifier))
                : rewritten;
        }

        public override SyntaxNode? VisitIdentifierName(
            IdentifierNameSyntax node)
        {
            if (!names.TryGetValue(
                    node.Identifier.ValueText,
                    out var name))
            {
                return base.VisitIdentifierName(node);
            }

            return SyntaxFactory.IdentifierName(name)
                .WithTriviaFrom(node);
        }

        public override SyntaxNode? VisitGenericName(
            GenericNameSyntax node)
        {
            var rewritten =
                (GenericNameSyntax)
                base.VisitGenericName(node)!;

            return names.TryGetValue(
                    node.Identifier.ValueText,
                    out var name)
                ? rewritten.WithIdentifier(
                    SyntaxFactory.Identifier(name)
                        .WithTriviaFrom(
                            rewritten.Identifier))
                : rewritten;
        }
    }

    private readonly record struct DestinationPlan(
        ITypeSymbol? MemberType,
        TypeMapperMapExistingKind MapExistingKind);
}
