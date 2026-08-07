using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MemberSurface;
using Morphant.Generator.Settings;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.PairConfiguration;

internal static class PairConfigurationModelBuilder
{
    public static MapperPairConfigurationModel Build(
        PairConfigurationDiscoveryModel discovery,
        MapperMappingPairModel mappingPairs,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Compilation is not CSharpCompilation compilation)
        {
            return new MapperPairConfigurationModel(
                mappingPairs,
                [mappingPairs],
                PairConfigurationSettings.Empty,
                [],
                [],
                HasInvalidBaseConfiguration: false);
        }

        var bindingMapperModels = discovery.Levels
            .Select(level =>
                MappingPairPipeline.BuildModel(
                    level.BindingRegistrations,
                    context,
                    cancellationToken))
            .Where(static model => model.HasValue)
            .Select(static model => model!.Value)
            .ToImmutableArray();
        var augmentedCompilation = BuildAugmentedCompilation(
            compilation,
            bindingMapperModels,
            cancellationToken);
        var knownSymbols = KnownSymbols.TryCreate(augmentedCompilation);

        if (knownSymbols is null)
        {
            return new MapperPairConfigurationModel(
                mappingPairs,
                [mappingPairs],
                PairConfigurationSettings.Empty,
                [],
                [],
                HasInvalidBaseConfiguration: false);
        }

        var levels =
            ImmutableArray.CreateBuilder<LocalMapperConfigurationLevel>(
                discovery.Levels.Length);
        var targetMapperType = augmentedCompilation.GetTypeByMetadataName(
                SymbolNameHelper.GetFullMetadataName(
                    discovery.ConfigureInfo.MapperType)) ??
            discovery.ConfigureInfo.MapperType;

        foreach (var level in discovery.Levels)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceSemanticModel = compilation.GetSemanticModel(
                level.ConfigureInfo.Syntax.SyntaxTree);
            var semanticModel = augmentedCompilation.GetSemanticModel(
                level.ConfigureInfo.Syntax.SyntaxTree);
            var declaringMapperType =
                level.ConfigureInfo.Syntax.Parent is
                    TypeDeclarationSyntax declaration &&
                semanticModel.GetDeclaredSymbol(
                    declaration,
                    cancellationToken) is INamedTypeSymbol declaredType
                    ? declaredType
                    : level.ConfigureInfo.MapperType;
            var localMappingPairs = MappingPairPipeline.BuildModel(
                level.InstantiatedRegistrations,
                context,
                cancellationToken);
            var localPairs =
                ImmutableArray.CreateBuilder<PairConfigurationModel>();

            if (localMappingPairs is { } localModel)
            {
                foreach (var pair in localModel.Pairs)
                {
                    var chain = FindRegistrationChain(
                        level.InvocationChains,
                        pair.Registration.Syntax);

                    localPairs.Add(
                        BuildPair(
                            pair,
                            chain,
                            semanticModel,
                            sourceSemanticModel,
                            MapperTypeSubstitution.Build(
                                level.ConfigureInfo.MapperType,
                                level.ConstructedMapperType),
                            knownSymbols,
                            augmentedCompilation,
                            targetMapperType,
                            declaringMapperType,
                            cancellationToken));
                }
            }

            levels.Add(
                new LocalMapperConfigurationLevel(
                    BuildRootSettings(
                        level.InvocationChains,
                        semanticModel,
                        knownSymbols,
                        cancellationToken),
                    localPairs.ToImmutable(),
                    level.BaseConfigureCalls));
        }

        return Compose(
            mappingPairs,
            ImmutableArray.Create(mappingPairs)
                .AddRange(bindingMapperModels),
            levels.ToImmutable(),
            discovery.HasUnavailableBaseConfiguration,
            compilation,
            augmentedCompilation,
            targetMapperType,
            cancellationToken);
    }

    private static CSharpCompilation BuildAugmentedCompilation(
        CSharpCompilation compilation,
        ImmutableArray<MapperMappingPairModel> mapperModels,
        CancellationToken cancellationToken)
    {
        var constructionRequests =
            ConstructionSurfacePipeline.BuildRequests(
                mapperModels,
                compilation,
                cancellationToken);
        var memberRequests = MemberSurfacePipeline.BuildRequests(
            mapperModels,
            compilation,
            cancellationToken);
        var parseOptions = (mapperModels.IsEmpty
                ? compilation.SyntaxTrees.FirstOrDefault()?.Options
                : mapperModels[0].ConfigureSyntax.SyntaxTree.Options) as
            CSharpParseOptions;
        var syntaxTrees =
            ImmutableArray.CreateBuilder<SyntaxTree>(
                constructionRequests.Length + memberRequests.Length);

        foreach (var request in constructionRequests)
        {
            syntaxTrees.Add(
                ParseGeneratedSource(
                    request.Source,
                    request.HintName,
                    parseOptions,
                    cancellationToken));
        }

        foreach (var request in memberRequests)
        {
            syntaxTrees.Add(
                ParseGeneratedSource(
                    request.Source,
                    request.HintName,
                    parseOptions,
                    cancellationToken));
        }

        return compilation.AddSyntaxTrees(syntaxTrees);
    }

    private static SyntaxTree ParseGeneratedSource(
        string source,
        string path,
        CSharpParseOptions? parseOptions,
        CancellationToken cancellationToken)
    {
        return CSharpSyntaxTree.ParseText(
            SourceText.From(source, Encoding.UTF8),
            parseOptions,
            path,
            cancellationToken);
    }

    private static MapperPairConfigurationModel Compose(
        MapperMappingPairModel mappingPairs,
        ImmutableArray<MapperMappingPairModel> surfaceMappingPairs,
        ImmutableArray<LocalMapperConfigurationLevel> levels,
        bool hasUnavailableBaseConfiguration,
        CSharpCompilation sourceCompilation,
        CSharpCompilation augmentedCompilation,
        INamedTypeSymbol targetMapperType,
        CancellationToken cancellationToken)
    {
        if (levels.IsEmpty)
        {
            return new MapperPairConfigurationModel(
                mappingPairs,
                surfaceMappingPairs,
                PairConfigurationSettings.Empty,
                [],
                [],
                hasUnavailableBaseConfiguration);
        }

        var effectivePairs =
            new Dictionary<MappingPairKey, PairConfigurationModel>();

        for (var levelIndex = levels.Length - 1;
             levelIndex >= 0;
             levelIndex--)
        {
            var level = levels[levelIndex];
            var basePairs = new Dictionary<MappingPairKey, PairConfigurationModel>(
                effectivePairs);
            var composedPairs =
                ImmutableArray.CreateBuilder<PairConfigurationModel>(
                    level.Pairs.Length);

            foreach (var localPair in level.Pairs)
            {
                var includeBase = localPair.Composition.IncludeBaseCalls
                    .FirstOrDefault();
                var includeBaseKey = includeBase == default
                    ? default
                    : MappingPairKey.Create(
                        includeBase.SourceType,
                        includeBase.DestinationType);
                PairConfigurationModel basePair = default;
                var hasBasePair = includeBase != default &&
                    basePairs.TryGetValue(includeBaseKey, out basePair);
                var composed = ComposePair(
                    localPair,
                    hasBasePair ? basePair : null,
                    hasConnectedBaseConfiguration:
                        levelIndex + 1 < levels.Length,
                    hasUnavailableBaseConfiguration &&
                        levelIndex + 1 == levels.Length,
                    sourceCompilation);

                composedPairs.Add(composed);
            }

            foreach (var composedPair in composedPairs)
            {
                effectivePairs[MappingPairKey.Create(composedPair.Pair)] =
                    composedPair;
            }
        }

        var pairs = ImmutableArray.CreateBuilder<PairConfigurationModel>(
            mappingPairs.Pairs.Length);

        foreach (var pair in mappingPairs.Pairs)
        {
            if (effectivePairs.TryGetValue(
                    MappingPairKey.Create(pair),
                    out var configuration))
            {
                pairs.Add(
                    AddAccessibilityConflict(
                        configuration with { Pair = pair },
                        augmentedCompilation,
                        targetMapperType,
                        cancellationToken));
            }
        }

        return new MapperPairConfigurationModel(
            mappingPairs,
            surfaceMappingPairs,
            levels[0].RootSettings,
            levels.Skip(1)
                .Select(static level => level.RootSettings)
                .ToImmutableArray(),
            pairs.ToImmutable(),
            hasUnavailableBaseConfiguration ||
            levels.Any(static level =>
                level.BaseConfigureCalls.Length > 1));
    }

    private static PairConfigurationModel ComposePair(
        PairConfigurationModel local,
        PairConfigurationModel? basePair,
        bool hasConnectedBaseConfiguration,
        bool hasUnavailableBaseConfiguration,
        CSharpCompilation compilation)
    {
        var includeBaseCalls = local.Composition.IncludeBaseCalls;
        var conflicts = local.Conflicts;

        if (includeBaseCalls.Length > 1)
        {
            conflicts |= PairConfigurationConflict.DuplicateIncludeBase;
        }

        if (includeBaseCalls.IsEmpty)
        {
            return local with
            {
                Composition = PairConfigurationCompositionModel.Empty,
                Conflicts = conflicts
            };
        }

        var includeBase = includeBaseCalls[0];

        if (!IsCompatibleBasePair(
                local.Pair,
                includeBase,
                compilation))
        {
            return local with
            {
                Composition = local.Composition with
                {
                    IncludedBaseSettings = []
                },
                Conflicts = conflicts |
                    PairConfigurationConflict.IncompatibleBasePair
            };
        }

        if (basePair is null)
        {
            conflicts |= hasConnectedBaseConfiguration
                ? PairConfigurationConflict.MissingBasePair
                : PairConfigurationConflict.MissingBaseConfiguration;

            if (hasUnavailableBaseConfiguration)
            {
                conflicts |=
                    PairConfigurationConflict.MissingBaseConfiguration;
            }

            return local with
            {
                Composition = local.Composition with
                {
                    IncludedBaseSettings = []
                },
                Conflicts = conflicts
            };
        }

        var inherited = basePair.Value;
        var localHasConvert = !local.Manual.Conversions.IsEmpty;
        DeclarativePairConfigurationModel declarative;

        if (localHasConvert)
        {
            declarative = local.Declarative;
            conflicts |= inherited.Conflicts &
                CompositionConflictMask;
        }
        else
        {
            declarative = new DeclarativePairConfigurationModel(
                local.Declarative.Constructs,
                inherited.Declarative.Members.AddRange(
                    local.Declarative.Members));
            conflicts |= inherited.Conflicts &
                IncludedMembersConflictMask;
        }

        var composition = local.Composition with
        {
            IncludedBaseSettings =
                ImmutableArray.Create(inherited.Settings)
                    .AddRange(
                        inherited.Composition.IncludedBaseSettings)
        };

        return local with
        {
            Declarative = declarative,
            Manual = local.Manual,
            Composition = composition,
            Conflicts = conflicts
        };
    }

    private const PairConfigurationConflict CompositionConflictMask =
        PairConfigurationConflict.DuplicateIncludeBase |
        PairConfigurationConflict.MissingBaseConfiguration |
        PairConfigurationConflict.MissingBasePair |
        PairConfigurationConflict.IncompatibleBasePair;

    private const PairConfigurationConflict IncludedMembersConflictMask =
        CompositionConflictMask |
        PairConfigurationConflict.DuplicateMembers;

    private static bool IsCompatibleBasePair(
        MappingPairModel pair,
        IncludeBaseConfigurationModel includeBase,
        CSharpCompilation compilation)
    {
        return IsBaseTypeConversion(
                   compilation.ClassifyConversion(
                       pair.SourceType,
                       includeBase.SourceType)) &&
               IsBaseTypeConversion(
                   compilation.ClassifyConversion(
                       pair.DestinationType,
                       includeBase.DestinationType));
    }

    private static bool IsBaseTypeConversion(Conversion conversion)
    {
        return conversion.IsIdentity ||
               conversion.IsImplicit &&
               (conversion.IsReference || conversion.IsBoxing);
    }

    private static PairConfigurationModel AddAccessibilityConflict(
        PairConfigurationModel model,
        CSharpCompilation compilation,
        INamedTypeSymbol targetMapperType,
        CancellationToken cancellationToken)
    {
        var accessible = model.Declarative.Constructs.All(static construct =>
                construct.Expression.IsAccessibleFromTargetMapper) &&
            AreEffectiveMembersAccessible(
                model.Declarative.Members,
                compilation,
                targetMapperType,
                cancellationToken) &&
            model.Manual.Conversions.All(static conversion =>
                conversion.Expression.IsAccessibleFromTargetMapper);

        return accessible
            ? model
            : model with
            {
                Conflicts = model.Conflicts |
                    PairConfigurationConflict.InaccessibleInheritedPlan
            };
    }

    private static bool AreEffectiveMembersAccessible(
        ImmutableArray<MembersConfigurationModel> configurations,
        CSharpCompilation compilation,
        INamedTypeSymbol targetMapperType,
        CancellationToken cancellationToken)
    {
        var overriddenNames = new HashSet<string>(StringComparer.Ordinal);

        for (var index = configurations.Length - 1;
             index >= 0;
             index--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var configuration = configurations[index];

            if (configuration.Expression.Syntax is not
                    LambdaExpressionSyntax lambda ||
                DeclarativeControlFlowPlanner.Build(
                    lambda,
                    configuration.Expression.SemanticModel,
                    cancellationToken) is not
                    DeclarativeControlFlowProgram program)
            {
                if (!configuration.Expression
                    .IsAccessibleFromTargetMapper)
                {
                    return false;
                }

                continue;
            }

            var assignments = EnumerateMemberAssignments(program.Root)
                .ToImmutableArray();
            var activeAssignments = assignments
                .Where(assignment =>
                    !overriddenNames.Contains(assignment.MemberName))
                .ToImmutableArray();

            if (!activeAssignments.IsEmpty &&
                !configuration.Expression.IsAccessibleFromTargetMapper)
            {
                var overriddenValues = new HashSet<SyntaxNode>(
                    assignments
                        .Where(assignment =>
                            overriddenNames.Contains(
                                assignment.MemberName))
                        .Select(static assignment =>
                            (SyntaxNode)assignment.Value));

                if (!IsExpressionAccessibleFromTargetMapper(
                        configuration.Expression.Syntax,
                        configuration.Expression.SemanticModel,
                        compilation,
                        targetMapperType,
                        configuration.Expression.DeclaringMapperType,
                        cancellationToken,
                        overriddenValues))
                {
                    return false;
                }
            }

            overriddenNames.UnionWith(
                assignments.Select(static assignment =>
                    assignment.MemberName));
        }

        return true;
    }

    private static IEnumerable<DeclarativeMemberAssignmentSyntax>
        EnumerateMemberAssignments(DeclarativeControlFlowSyntaxNode node)
    {
        switch (node)
        {
            case DeclarativeLeafSyntaxNode leaf:
                return leaf.MemberAssignments;

            case DeclarativeLocalDeclarationsSyntaxNode locals:
                return EnumerateMemberAssignments(locals.Next);

            case DeclarativeEvaluationSyntaxNode evaluation:
                return EnumerateMemberAssignments(evaluation.Next);

            case DeclarativeConditionalSyntaxNode conditional:
                return EnumerateMemberAssignments(conditional.WhenTrue)
                    .Concat(
                        EnumerateMemberAssignments(
                            conditional.WhenFalse));

            case DeclarativeSwitchSyntaxNode switchNode:
                var assignments = switchNode.Sections.SelectMany(section =>
                    EnumerateMemberAssignments(section.Branch));

                return switchNode.Continuation is { } continuation
                    ? assignments.Concat(
                        EnumerateMemberAssignments(continuation))
                    : assignments;

            default:
                return [];
        }
    }

    private static PairConfigurationSettings BuildRootSettings(
        ImmutableArray<PairConfigurationInvocationChain> chains,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken)
    {
        var settings = PairConfigurationSettings.Empty;

        foreach (var chain in chains)
        {
            var reachedMap = false;

            foreach (var invocation in chain.Invocations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (semanticModel.GetSymbolInfo(
                        invocation,
                        cancellationToken).Symbol is not IMethodSymbol method)
                {
                    continue;
                }

                if (IsMapperBuilderMapMethod(method, knownSymbols))
                {
                    reachedMap = true;
                    continue;
                }

                if (!reachedMap)
                {
                    ApplySetting(
                        invocation,
                        method,
                        semanticModel,
                        knownSymbols,
                        cancellationToken,
                        rootLevel: true,
                        ref settings);
                }
            }
        }

        return settings;
    }

    private static PairConfigurationInvocationChain FindRegistrationChain(
        ImmutableArray<PairConfigurationInvocationChain> chains,
        InvocationExpressionSyntax registration)
    {
        foreach (var chain in chains)
        {
            if (chain.Invocations.Any(invocation =>
                    invocation.SyntaxTree == registration.SyntaxTree &&
                    invocation.Span == registration.Span))
            {
                return chain;
            }
        }

        return default;
    }

    private static PairConfigurationModel BuildPair(
        MappingPairModel pair,
        PairConfigurationInvocationChain chain,
        SemanticModel semanticModel,
        SemanticModel sourceSemanticModel,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol>
            mapperTypeSubstitutions,
        KnownSymbols knownSymbols,
        CSharpCompilation compilation,
        INamedTypeSymbol targetMapperType,
        INamedTypeSymbol declaringMapperType,
        CancellationToken cancellationToken)
    {
        var settings = BuildMapMappingMode(
            pair.Registration.Syntax,
            semanticModel,
            cancellationToken);
        var constructs =
            ImmutableArray.CreateBuilder<ConstructConfigurationModel>();
        var members =
            ImmutableArray.CreateBuilder<MembersConfigurationModel>();
        var conversions =
            ImmutableArray.CreateBuilder<ConvertConfigurationModel>();
        var includeBaseCalls =
            ImmutableArray.CreateBuilder<IncludeBaseConfigurationModel>();
        var reachedRegistration = false;

        foreach (var invocation in chain.Invocations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!reachedRegistration)
            {
                reachedRegistration =
                    invocation.SyntaxTree == pair.Registration.Syntax.SyntaxTree &&
                    invocation.Span == pair.Registration.Syntax.Span;
                continue;
            }

            if (semanticModel.GetSymbolInfo(
                    invocation,
                    cancellationToken).Symbol is not IMethodSymbol method)
            {
                continue;
            }

            if (ApplySetting(
                    invocation,
                    method,
                    semanticModel,
                    knownSymbols,
                    cancellationToken,
                    rootLevel: false,
                    ref settings))
            {
                continue;
            }

            if (IsIncludeBaseMethod(method))
            {
                if (TryBuildIncludeBaseConfiguration(
                        invocation,
                        sourceSemanticModel,
                        mapperTypeSubstitutions,
                        cancellationToken,
                        out var includeBase))
                {
                    includeBaseCalls.Add(includeBase);
                }

                continue;
            }

            if (!IsGeneratedConfigurationMethod(method))
            {
                continue;
            }

            var expression = TryBindConfigurationExpression(
                invocation,
                method,
                semanticModel,
                compilation,
                targetMapperType,
                declaringMapperType,
                cancellationToken);

            if (expression is null)
            {
                continue;
            }

            switch (method.Name)
            {
                case "Construct":
                    constructs.Add(
                        new ConstructConfigurationModel(
                            invocation,
                            expression.DelegateInvokeMethod.Parameters.Length == 1
                                ? ConstructConfigurationForm.Source
                                : ConstructConfigurationForm.SourceAndPrevious,
                            expression));
                    break;

                case "Members":
                    members.Add(
                        new MembersConfigurationModel(
                            invocation,
                            expression.DelegateInvokeMethod.Parameters.Length == 2
                                ? MembersConfigurationForm.SourceAndPrevious
                                : MembersConfigurationForm
                                    .SourcePreviousAndResult,
                            expression));
                    break;

                case "Convert":
                    conversions.Add(
                        new ConvertConfigurationModel(
                            invocation,
                            expression));
                    break;
            }
        }

        var immutableConstructs = constructs.ToImmutable();
        var immutableMembers = members.ToImmutable();
        var immutableConversions = conversions.ToImmutable();
        var conflicts = PairConfigurationConflict.None;

        if (immutableConstructs.Length > 1)
        {
            conflicts |= PairConfigurationConflict.DuplicateConstruct;
        }

        if (immutableMembers.Length > 1)
        {
            conflicts |= PairConfigurationConflict.DuplicateMembers;
        }

        if (immutableConversions.Length > 1)
        {
            conflicts |= PairConfigurationConflict.DuplicateConvert;
        }

        if (immutableConversions.Length > 0 &&
            (immutableConstructs.Length > 0 || immutableMembers.Length > 0))
        {
            conflicts |= PairConfigurationConflict.MixedManualAndDeclarative;
        }

        return new PairConfigurationModel(
            pair,
            settings,
            new DeclarativePairConfigurationModel(
                immutableConstructs,
                immutableMembers),
            new ManualPairConfigurationModel(immutableConversions),
            new PairConfigurationCompositionModel(
                includeBaseCalls.ToImmutable(),
                []),
            conflicts);
    }

    private static bool TryBuildIncludeBaseConfiguration(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol>
            mapperTypeSubstitutions,
        CancellationToken cancellationToken,
        out IncludeBaseConfigurationModel configuration)
    {
        var genericName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax memberName
            } => memberName,
            GenericNameSyntax directName => directName,
            _ => null
        };

        if (genericName?.TypeArgumentList.Arguments is not
                { Count: 2 } arguments ||
            semanticModel.GetTypeInfo(
                arguments[0],
                cancellationToken).Type is not { } sourceType ||
            semanticModel.GetTypeInfo(
                arguments[1],
                cancellationToken).Type is not { } destinationType)
        {
            configuration = default;
            return false;
        }

        configuration = new IncludeBaseConfigurationModel(
            invocation,
            MapperTypeSubstitution.Substitute(
                sourceType,
                mapperTypeSubstitutions,
                semanticModel.Compilation),
            MapperTypeSubstitution.Substitute(
                destinationType,
                mapperTypeSubstitutions,
                semanticModel.Compilation));
        return true;
    }

    private static PairConfigurationSettings BuildMapMappingMode(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        PairConfigurationSetting<MappingModeValue> setting;

        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            setting = new PairConfigurationSetting<MappingModeValue>(
                invocation,
                MappingModeValue.Default,
                PairConfigurationSettingOrigin.Implicit);
        }
        else
        {
            setting = new PairConfigurationSetting<MappingModeValue>(
                invocation,
                TryGetMappingMode(
                    invocation.ArgumentList.Arguments[0].Expression,
                    semanticModel,
                    cancellationToken,
                    out var value)
                    ? value
                    : null,
                PairConfigurationSettingOrigin.Explicit);
        }

        return PairConfigurationSettings.Empty with
        {
            MappingMode = setting
        };
    }

    private static bool ApplySetting(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel semanticModel,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken,
        bool rootLevel,
        ref PairConfigurationSettings settings)
    {
        if (rootLevel && IsMapperBuilderMappingModeMethod(method, knownSymbols))
        {
            settings = settings with
            {
                MappingMode = BuildSetting<MappingModeValue>(
                    invocation,
                    semanticModel,
                    cancellationToken,
                    TryGetMappingMode)
            };
            return true;
        }

        if (!IsMapperBuilderBaseSettingMethod(method, knownSymbols))
        {
            return false;
        }

        switch (method.Name)
        {
            case "NullSourceHandling":
                settings = settings with
                {
                    NullSourceHandling =
                        BuildSetting<NullSourceHandlingValue>(
                            invocation,
                            semanticModel,
                            cancellationToken,
                            TryGetDefinedEnum)
                };
                return true;

            case "NullDestinationHandling":
                settings = settings with
                {
                    NullDestinationHandling =
                        BuildSetting<NullDestinationHandlingValue>(
                            invocation,
                            semanticModel,
                            cancellationToken,
                            TryGetDefinedEnum)
                };
                return true;

            case "ConstructorSelection":
                settings = settings with
                {
                    ConstructorSelection =
                        BuildSetting<ConstructorSelectionValue>(
                            invocation,
                            semanticModel,
                            cancellationToken,
                            TryGetDefinedEnum)
                };
                return true;

            case "MemberSelection":
                settings = settings with
                {
                    MemberSelection =
                        BuildSetting<MemberSelectionValue>(
                            invocation,
                            semanticModel,
                            cancellationToken,
                            TryGetDefinedEnum)
                };
                return true;

            case "UnmappedMemberValidation":
                settings = settings with
                {
                    UnmappedMemberValidation =
                        BuildSetting<UnmappedMemberValidationValue>(
                            invocation,
                            semanticModel,
                            cancellationToken,
                            TryGetDefinedEnum)
                };
                return true;

            default:
                return false;
        }
    }

    private static PairConfigurationSetting<TValue> BuildSetting<TValue>(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        TryParseSetting<TValue> tryParse)
        where TValue : struct, Enum
    {
        TValue? value = null;

        if (invocation.ArgumentList.Arguments.Count == 1 &&
            tryParse(
                invocation.ArgumentList.Arguments[0].Expression,
                semanticModel,
                cancellationToken,
                out var parsedValue))
        {
            value = parsedValue;
        }

        return new PairConfigurationSetting<TValue>(
            invocation,
            value,
            PairConfigurationSettingOrigin.Explicit);
    }

    private static BoundConfigurationExpression?
        TryBindConfigurationExpression(
            InvocationExpressionSyntax invocation,
            IMethodSymbol method,
            SemanticModel semanticModel,
            CSharpCompilation compilation,
            INamedTypeSymbol targetMapperType,
            INamedTypeSymbol declaringMapperType,
            CancellationToken cancellationToken)
    {
        if (invocation.ArgumentList.Arguments.Count != 1 ||
            method.Parameters.LastOrDefault()?.Type is not
                INamedTypeSymbol delegateType ||
            delegateType.DelegateInvokeMethod is not { } delegateInvokeMethod)
        {
            return null;
        }

        var syntax = invocation.ArgumentList.Arguments[0].Expression;

        return new BoundConfigurationExpression(
            syntax,
            semanticModel,
            semanticModel.GetOperation(syntax, cancellationToken),
            delegateType,
            delegateInvokeMethod,
            declaringMapperType,
            IsExpressionAccessibleFromTargetMapper(
                syntax,
                semanticModel,
                compilation,
                targetMapperType,
                declaringMapperType,
                cancellationToken));
    }

    private static bool IsIncludeBaseMethod(IMethodSymbol method)
    {
        return method.Name == "IncludeBase" &&
               method.MethodKind == MethodKind.Ordinary &&
               !method.IsStatic &&
               method.Parameters.Length == 0 &&
               method.TypeArguments.Length == 2 &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       method.ContainingType.OriginalDefinition),
                   "Morphant.MapperBuilder`2");
    }

    private static bool IsExpressionAccessibleFromTargetMapper(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CSharpCompilation compilation,
        INamedTypeSymbol targetMapperType,
        INamedTypeSymbol declaringMapperType,
        CancellationToken cancellationToken,
        ISet<SyntaxNode>? excludedSubtrees = null)
    {
        if (SymbolEqualityComparer.Default.Equals(
                targetMapperType,
                declaringMapperType))
        {
            return true;
        }

        bool DescendInto(SyntaxNode node) =>
            excludedSubtrees is null ||
            !excludedSubtrees.Contains(node);

        if (expression.DescendantNodesAndSelf(DescendInto)
            .Any(static node => node is BaseExpressionSyntax))
        {
            return false;
        }

        foreach (var name in expression.DescendantNodesAndSelf(DescendInto)
                     .OfType<SimpleNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var symbol = semanticModel.GetSymbolInfo(
                    name,
                    cancellationToken)
                .Symbol;

            if (symbol is null ||
                symbol is ILocalSymbol or IParameterSymbol or
                    ITypeParameterSymbol ||
                symbol.ContainingType is null ||
                !IsMapperHierarchyMember(
                    symbol,
                    declaringMapperType))
            {
                continue;
            }

            if (!compilation.IsSymbolAccessibleWithin(
                    symbol,
                    targetMapperType))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsMapperHierarchyMember(
        ISymbol symbol,
        INamedTypeSymbol mapperType)
    {
        for (var current = mapperType;
             current is not null;
             current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    symbol.ContainingType.OriginalDefinition,
                    current.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGeneratedConfigurationMethod(IMethodSymbol method)
    {
        var definition = method.ReducedFrom ?? method;

        return method.Name is "Construct" or "Members" or "Convert" &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       definition.ContainingType),
                   MetadataNames.GeneratedMappingExtensions);
    }

    private static bool IsMapperBuilderMapMethod(
        IMethodSymbol method,
        KnownSymbols knownSymbols)
    {
        return method.Name == "Map" &&
               method.MethodKind == MethodKind.Ordinary &&
               !method.IsStatic &&
               method.Parameters.Length == 1 &&
               method.TypeArguments.Length == 2 &&
               SymbolEqualityComparer.Default.Equals(
                   method.ContainingType,
                   knownSymbols.MapperBuilder);
    }

    private static bool IsMapperBuilderMappingModeMethod(
        IMethodSymbol method,
        KnownSymbols knownSymbols)
    {
        return method.Name == "MappingMode" &&
               method.MethodKind == MethodKind.Ordinary &&
               !method.IsStatic &&
               method.Parameters.Length == 1 &&
               method.TypeArguments.Length == 0 &&
               SymbolEqualityComparer.Default.Equals(
                   method.ContainingType,
                   knownSymbols.MapperBuilder);
    }

    private static bool IsMapperBuilderBaseSettingMethod(
        IMethodSymbol method,
        KnownSymbols knownSymbols)
    {
        return method.Name is
                   "NullSourceHandling" or
                   "NullDestinationHandling" or
                   "ConstructorSelection" or
                   "MemberSelection" or
                   "UnmappedMemberValidation" &&
               method.MethodKind == MethodKind.Ordinary &&
               !method.IsStatic &&
               method.Parameters.Length == 1 &&
               method.TypeArguments.Length == 0 &&
               SymbolEqualityComparer.Default.Equals(
                   method.ContainingType.OriginalDefinition,
                   knownSymbols.MapperBuilderBase);
    }

    private static bool TryGetMappingMode(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out MappingModeValue value)
    {
        if (!TryGetInt32Constant(
                expression,
                semanticModel,
                cancellationToken,
                out var numericValue) ||
            (numericValue & ~(int)MappingModeValue.CreateAndUpdate) != 0)
        {
            value = default;
            return false;
        }

        value = (MappingModeValue)numericValue;
        return true;
    }

    private static bool TryGetDefinedEnum<TValue>(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out TValue value)
        where TValue : struct, Enum
    {
        if (!TryGetInt32Constant(
                expression,
                semanticModel,
                cancellationToken,
                out var numericValue) ||
            !Enum.IsDefined(typeof(TValue), numericValue))
        {
            value = default;
            return false;
        }

        value = (TValue)Enum.ToObject(typeof(TValue), numericValue);
        return true;
    }

    private static bool TryGetInt32Constant(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out int value)
    {
        var constant = semanticModel.GetConstantValue(
            expression,
            cancellationToken);

        if (!constant.HasValue || constant.Value is not int parsedValue)
        {
            value = default;
            return false;
        }

        value = parsedValue;
        return true;
    }

    private delegate bool TryParseSetting<TValue>(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out TValue value)
        where TValue : struct, Enum;

    private readonly record struct LocalMapperConfigurationLevel(
        PairConfigurationSettings RootSettings,
        ImmutableArray<PairConfigurationModel> Pairs,
        ImmutableArray<InvocationExpressionSyntax> BaseConfigureCalls);

    private readonly record struct MappingPairKey(
        string Source,
        string Destination)
    {
        public static MappingPairKey Create(MappingPairModel pair) =>
            Create(pair.SourceType, pair.DestinationType);

        public static MappingPairKey Create(
            ITypeSymbol source,
            ITypeSymbol destination) =>
            new(
                MappingTypeIdentityPolicy.Create(source).Key,
                MappingTypeIdentityPolicy.Create(destination).Key);
    }
}
