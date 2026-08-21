using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.ConstructionSurface;
using Morphant.Generator.MappingPair;
using Morphant.Generator.MapperDeclaration;
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

        var mapperDeclaration = discovery.ConfigureInfo.Declaration ??
            throw new InvalidOperationException(
                "The root mapper configuration must have a declaration model.");

        if (context.Compilation is not CSharpCompilation compilation)
        {
            return new MapperPairConfigurationModel(
                mapperDeclaration,
                mappingPairs,
                ImmutableArray.Create<MapperMappingPairModel>(mappingPairs),
                PairConfigurationSettings.Empty,
                ImmutableArray<PairConfigurationSettings>.Empty,
                ImmutableArray<DuplicateBaseConfigurationCallModel>.Empty,
                ImmutableArray<PairConfigurationModel>.Empty,
                HasInvalidBaseConfiguration: false,
                discovery.UnavailableBaseConfigurations,
                discovery.FlowBreaks);
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
        var augmentedCompilation = RequiresAugmentedCompilation(discovery)
            ? BuildAugmentedCompilation(
                compilation,
                bindingMapperModels,
                cancellationToken)
            : compilation;
        var knownSymbols = KnownSymbols.TryCreate(augmentedCompilation);

        if (knownSymbols is null)
        {
            return new MapperPairConfigurationModel(
                mapperDeclaration,
                mappingPairs,
                ImmutableArray.Create<MapperMappingPairModel>(mappingPairs),
                PairConfigurationSettings.Empty,
                ImmutableArray<PairConfigurationSettings>.Empty,
                ImmutableArray<DuplicateBaseConfigurationCallModel>.Empty,
                ImmutableArray<PairConfigurationModel>.Empty,
                HasInvalidBaseConfiguration: false,
                discovery.UnavailableBaseConfigurations,
                discovery.FlowBreaks);
        }

        var levels =
            ImmutableArray.CreateBuilder<LocalMapperConfigurationLevel>(
                discovery.Levels.Length);
        var targetMapperType = augmentedCompilation.GetTypeByMetadataName(
                SymbolNameHelper.GetFullMetadataName(
                    discovery.ConfigureInfo.MapperType)) ??
            discovery.ConfigureInfo.MapperType;

        for (var levelOrder = 0;
             levelOrder < discovery.Levels.Length;
             levelOrder++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var level = discovery.Levels[levelOrder];

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
                            FindDeclaredRegistration(
                                level.BindingRegistrations,
                                pair.Registration.Syntax),
                            chain,
                            semanticModel,
                            sourceSemanticModel,
                            MapperTypeSubstitution.Build(
                                level.ConfigureInfo.MapperType,
                                level.ConstructedMapperType),
                            knownSymbols,
                            augmentedCompilation,
                            targetMapperType,
                            compilation,
                            discovery.ConfigureInfo.MapperType,
                            declaringMapperType,
                            level.ConstructedMapperType,
                            levelOrder,
                            cancellationToken));
                }
            }

            levels.Add(
                new LocalMapperConfigurationLevel(
                    declaringMapperType,
                    BuildRootSettings(
                        level.InvocationChains,
                        semanticModel,
                        knownSymbols,
                        cancellationToken),
                    localPairs.ToImmutable(),
                    localMappingPairs is { } candidateModel
                        ? BuildCandidates(
                            candidateModel,
                            localPairs)
                        : ImmutableArray<PairConfigurationCandidateModel>.Empty,
                    level.BaseConfigureCalls));
        }

        return Compose(
            mapperDeclaration,
            mappingPairs,
            ImmutableArray.Create(mappingPairs)
                .AddRange(bindingMapperModels),
            levels.ToImmutable(),
            discovery.HasInvalidBaseConfiguration,
            discovery.UnavailableBaseConfigurations,
            discovery.FlowBreaks,
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
        var pairs = CanonicalMappingPairSelector.Select(
            mapperModels,
            cancellationToken);
        var constructionRequests =
            ConstructionSurfacePipeline.BuildRequests(
                pairs,
                compilation,
                cancellationToken);
        var memberRequests = MemberSurfacePipeline.BuildRequests(
            pairs,
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

    private static bool RequiresAugmentedCompilation(
        PairConfigurationDiscoveryModel discovery)
    {
        return discovery.Levels.Any(static level =>
            level.InvocationChains.Any(static chain =>
                chain.Invocations.Length > 1));
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
        MapperDeclarationInfo declaration,
        MapperMappingPairModel mappingPairs,
        ImmutableArray<MapperMappingPairModel> surfaceMappingPairs,
        ImmutableArray<LocalMapperConfigurationLevel> levels,
        bool hasInvalidBaseConfiguration,
        ImmutableArray<UnavailableBaseConfigurationModel>
            unavailableBaseConfigurations,
        ImmutableArray<BuilderFlowBreakModel> flowBreaks,
        CSharpCompilation sourceCompilation,
        CSharpCompilation augmentedCompilation,
        INamedTypeSymbol targetMapperType,
        CancellationToken cancellationToken)
    {
        if (levels.IsEmpty)
        {
            return new MapperPairConfigurationModel(
                declaration,
                mappingPairs,
                surfaceMappingPairs,
                PairConfigurationSettings.Empty,
                ImmutableArray<PairConfigurationSettings>.Empty,
                ImmutableArray<DuplicateBaseConfigurationCallModel>.Empty,
                ImmutableArray<PairConfigurationModel>.Empty,
                hasInvalidBaseConfiguration,
                unavailableBaseConfigurations,
                flowBreaks);
        }

        var effectiveCandidates =
            new Dictionary<MappingPairKey, PairConfigurationCandidateModel>();

        for (var levelIndex = levels.Length - 1;
             levelIndex >= 0;
             levelIndex--)
        {
            var level = levels[levelIndex];
            var inheritedCandidates =
                new Dictionary<MappingPairKey,
                    PairConfigurationCandidateModel>(effectiveCandidates);
            var localPairs = level.Pairs.ToDictionary(
                static pair => MappingPairKey.Create(pair.Pair));
            var localCandidates = level.Candidates.ToDictionary(
                static candidate => candidate.Key);
            var composedLocalPairs =
                new Dictionary<MappingPairKey, PairConfigurationModel>();
            var composedPairs =
                ImmutableArray.CreateBuilder<PairConfigurationModel>(
                    level.Pairs.Length);

            foreach (var localPair in level.Pairs)
            {
                composedPairs.Add(ComposeLevelPair(
                    localPair,
                    localPairs,
                    localCandidates,
                    inheritedCandidates,
                    composedLocalPairs,
                    hasUnknownBaseConfiguration:
                        hasInvalidBaseConfiguration,
                    sourceCompilation));
            }

            foreach (var candidate in level.Candidates)
            {
                effectiveCandidates[candidate.Key] =
                    !candidate.IsCategory3Invalid &&
                    composedLocalPairs.TryGetValue(
                        candidate.Key,
                        out var composedPair)
                        ? candidate with { Configuration = composedPair }
                        : candidate;
            }
        }

        var pairs = ImmutableArray.CreateBuilder<PairConfigurationModel>(
            mappingPairs.Pairs.Length);

        foreach (var pair in mappingPairs.Pairs)
        {
            if (effectiveCandidates.TryGetValue(
                    MappingPairKey.Create(pair),
                    out var candidate) &&
                candidate.Configuration is { } configuration)
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
            declaration,
            mappingPairs,
            surfaceMappingPairs,
            levels[0].RootSettings,
            levels.Skip(1)
                .Select(static level => level.RootSettings)
                .ToImmutableArray(),
            BuildDuplicateBaseConfigurationCalls(levels),
            pairs.ToImmutable(),
            hasInvalidBaseConfiguration ||
            levels.Any(static level =>
                level.BaseConfigureCalls.Length > 1),
            unavailableBaseConfigurations,
            flowBreaks);
    }

    private static PairConfigurationModel ComposeLevelPair(
        PairConfigurationModel local,
        IReadOnlyDictionary<MappingPairKey, PairConfigurationModel> localPairs,
        IReadOnlyDictionary<MappingPairKey, PairConfigurationCandidateModel>
            localCandidates,
        IReadOnlyDictionary<MappingPairKey, PairConfigurationCandidateModel>
            inheritedCandidates,
        IDictionary<MappingPairKey, PairConfigurationModel> composedLocalPairs,
        bool hasUnknownBaseConfiguration,
        CSharpCompilation compilation)
    {
        var localKey = MappingPairKey.Create(local.Pair);

        if (composedLocalPairs.TryGetValue(localKey, out var cached))
        {
            return cached;
        }

        var includeBaseCalls = local.Composition.IncludeBaseCalls;
        var lookup = IncludeBaseLookupResult.None;

        if (includeBaseCalls.Length == 1)
        {
            var includeBase = includeBaseCalls[0];
            var includeBaseKey = MappingPairKey.Create(
                includeBase.SourceType,
                includeBase.DestinationType);

            if (includeBaseKey != localKey &&
                localCandidates.TryGetValue(
                    includeBaseKey,
                    out var localCandidate))
            {
                if (localCandidate.IsCategory3Invalid)
                {
                    lookup = IncludeBaseLookupResult.Invalid;
                }
                else if (!IsCompatibleBasePair(
                             local.Pair,
                             includeBase,
                             compilation))
                {
                    lookup = IncludeBaseLookupResult.Found(null);
                }
                else if (localPairs.TryGetValue(
                             includeBaseKey,
                             out var localBasePair))
                {
                    lookup = IncludeBaseLookupResult.Found(
                        ComposeLevelPair(
                            localBasePair,
                            localPairs,
                            localCandidates,
                            inheritedCandidates,
                            composedLocalPairs,
                            hasUnknownBaseConfiguration,
                            compilation));
                }
            }
            else if (inheritedCandidates.TryGetValue(
                         includeBaseKey,
                         out var inheritedCandidate))
            {
                lookup = inheritedCandidate.IsCategory3Invalid
                    ? IncludeBaseLookupResult.Invalid
                    : IncludeBaseLookupResult.Found(
                        inheritedCandidate.Configuration);
            }
            else
            {
                lookup = hasUnknownBaseConfiguration
                    ? IncludeBaseLookupResult.Unknown
                    : IncludeBaseLookupResult.Missing;
            }
        }

        var composed = ComposePair(
            local,
            lookup,
            compilation);

        composedLocalPairs[localKey] = composed;
        return composed;
    }

    private static PairConfigurationModel ComposePair(
        PairConfigurationModel local,
        IncludeBaseLookupResult lookup,
        CSharpCompilation compilation)
    {
        var includeBaseCalls = local.Composition.IncludeBaseCalls;
        var conflicts = local.Conflicts;

        if (includeBaseCalls.Length > 1)
        {
            var first = includeBaseCalls[0];
            var issues = includeBaseCalls.Skip(1)
                .Select(includeBase =>
                    new InheritanceCompositionIssueModel(
                        InheritanceCompositionIssueKind
                            .DuplicateIncludeBase,
                        local.Origin,
                        includeBase,
                        first.Invocation))
                .ToImmutableArray();

            return local with
            {
                Composition = local.Composition with
                {
                    IncludedBaseSettings = ImmutableArray<PairConfigurationSettings>.Empty,
                    Issues = issues,
                    InaccessibleCallbacks = ImmutableArray<InheritedCallbackAccessibilityModel>.Empty
                },
                Conflicts = conflicts |
                    PairConfigurationConflict.DuplicateIncludeBase
            };
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

        if (lookup.Status == IncludeBaseLookupStatus.Missing)
        {
            return local with
            {
                Composition = local.Composition with
                {
                    IncludedBaseSettings = ImmutableArray<PairConfigurationSettings>.Empty,
                    Issues =
                    ImmutableArray.Create<InheritanceCompositionIssueModel>(
                        new InheritanceCompositionIssueModel(
                            InheritanceCompositionIssueKind
                                .MissingIncludedPair,
                            local.Origin,
                            includeBase)
                    ),
                    InaccessibleCallbacks = ImmutableArray<InheritedCallbackAccessibilityModel>.Empty
                },
                Conflicts = conflicts |
                    PairConfigurationConflict.MissingBasePair
            };
        }

        if (lookup.Status == IncludeBaseLookupStatus.Unknown)
        {
            return local with
            {
                Composition = local.Composition with
                {
                    IncludedBaseSettings = ImmutableArray<PairConfigurationSettings>.Empty,
                    Issues = ImmutableArray<InheritanceCompositionIssueModel>.Empty,
                    InaccessibleCallbacks = ImmutableArray<InheritedCallbackAccessibilityModel>.Empty
                },
                Conflicts = conflicts |
                    PairConfigurationConflict.MissingBaseConfiguration
            };
        }

        if (lookup.Status == IncludeBaseLookupStatus.Invalid)
        {
            return local with
            {
                Composition = local.Composition with
                {
                    IncludedBaseSettings = ImmutableArray<PairConfigurationSettings>.Empty,
                    Issues =
                    ImmutableArray.Create<InheritanceCompositionIssueModel>(
                        new InheritanceCompositionIssueModel(
                            InheritanceCompositionIssueKind
                                .InvalidIncludedPair,
                            local.Origin,
                            includeBase)
                    ),
                    InaccessibleCallbacks = ImmutableArray<InheritedCallbackAccessibilityModel>.Empty
                },
                Conflicts = conflicts |
                    PairConfigurationConflict.InvalidBasePair
            };
        }

        var sourceCompatible = IsBaseTypeConversion(
            compilation.ClassifyConversion(
                local.Pair.SourceType,
                includeBase.SourceType));
        var destinationCompatible = IsBaseTypeConversion(
            compilation.ClassifyConversion(
                local.Pair.DestinationType,
                includeBase.DestinationType));

        if (!sourceCompatible || !destinationCompatible)
        {
            var issues = ImmutableArray.CreateBuilder<
                InheritanceCompositionIssueModel>(2);

            if (!sourceCompatible)
            {
                issues.Add(new InheritanceCompositionIssueModel(
                    InheritanceCompositionIssueKind.IncompatibleSource,
                    local.Origin,
                    includeBase));
            }

            if (!destinationCompatible)
            {
                issues.Add(new InheritanceCompositionIssueModel(
                    InheritanceCompositionIssueKind.IncompatibleDestination,
                    local.Origin,
                    includeBase));
            }

            return local with
            {
                Composition = local.Composition with
                {
                    IncludedBaseSettings = ImmutableArray<PairConfigurationSettings>.Empty,
                    Issues = issues.ToImmutable(),
                    InaccessibleCallbacks = ImmutableArray<InheritedCallbackAccessibilityModel>.Empty
                },
                Conflicts = conflicts |
                    PairConfigurationConflict.IncompatibleBasePair
            };
        }

        var inherited = lookup.Configuration ??
            throw new InvalidOperationException(
                "A compatible IncludeBase candidate must have a model.");
        var localHasConvert = !local.Manual.Conversions.IsEmpty;
        var localHasDeclarative =
            !local.Declarative.ResultPolicies.IsEmpty ||
            !local.Declarative.Members.IsEmpty ||
            !local.Declarative.IncludeMembers.IsEmpty;
        var exactSamePair =
            MappingPairKey.Create(local.Pair) ==
            MappingPairKey.Create(inherited.Pair);
        DeclarativePairConfigurationModel declarative;
        ManualPairConfigurationModel manual;

        if (!exactSamePair)
        {
            if (localHasConvert)
            {
                declarative = local.Declarative;
                conflicts |= inherited.Conflicts &
                    CompositionConflictMask;
            }
            else
            {
                declarative = new DeclarativePairConfigurationModel(
                    local.Declarative.ResultPolicies,
                    inherited.Declarative.Members.AddRange(
                        local.Declarative.Members),
                    inherited.Declarative.IncludeMembers.AddRange(
                        local.Declarative.IncludeMembers));
                conflicts |= inherited.Conflicts &
                    IncludedMembersConflictMask;
            }

            manual = local.Manual;
        }
        else if (localHasConvert)
        {
            declarative = local.Declarative;
            manual = local.Manual;
            conflicts |= inherited.Conflicts &
                CompositionConflictMask;
        }
        else if (localHasDeclarative)
        {
            var localHasResultPolicy =
                !local.Declarative.ResultPolicies.IsEmpty;

            declarative = new DeclarativePairConfigurationModel(
                localHasResultPolicy
                    ? local.Declarative.ResultPolicies
                    : inherited.Declarative.ResultPolicies,
                inherited.Declarative.Members.AddRange(
                    local.Declarative.Members),
                inherited.Declarative.IncludeMembers.AddRange(
                    local.Declarative.IncludeMembers));
            manual = local.Manual;
            conflicts |= inherited.Conflicts &
                (CompositionConflictMask |
                 PairConfigurationConflict.DuplicateMembers |
                 (localHasResultPolicy
                     ? PairConfigurationConflict.None
                     : PairConfigurationConflict.DuplicateResultPolicy));
        }
        else
        {
            declarative = inherited.Declarative;
            manual = inherited.Manual;
            conflicts |= inherited.Conflicts;
        }

        var composition = local.Composition with
        {
            IncludedBaseSettings =
                ImmutableArray.Create(inherited.Settings)
                    .AddRange(
                        inherited.Composition.IncludedBaseSettings),
            Issues = FilterInheritedIssues(
                inherited.Composition.Issues,
                conflicts),
            InaccessibleCallbacks = ImmutableArray<InheritedCallbackAccessibilityModel>.Empty
        };

        return local with
        {
            Declarative = declarative,
            Manual = manual,
            Composition = composition,
            Conflicts = conflicts
        };
    }

    private const PairConfigurationConflict CompositionConflictMask =
        PairConfigurationConflict.DuplicateIncludeBase |
        PairConfigurationConflict.MissingBaseConfiguration |
        PairConfigurationConflict.MissingBasePair |
        PairConfigurationConflict.IncompatibleBasePair |
        PairConfigurationConflict.InvalidBasePair;

    private const PairConfigurationConflict IncludedMembersConflictMask =
        CompositionConflictMask |
        PairConfigurationConflict.DuplicateMembers;

    private static ImmutableArray<DuplicateBaseConfigurationCallModel>
        BuildDuplicateBaseConfigurationCalls(
            ImmutableArray<LocalMapperConfigurationLevel> levels)
    {
        var result = ImmutableArray.CreateBuilder<
            DuplicateBaseConfigurationCallModel>();

        for (var levelOrder = 0;
             levelOrder < levels.Length;
             levelOrder++)
        {
            var level = levels[levelOrder];

            if (level.BaseConfigureCalls.Length < 2)
            {
                continue;
            }

            var first = level.BaseConfigureCalls[0];

            foreach (var duplicate in level.BaseConfigureCalls.Skip(1))
            {
                result.Add(new DuplicateBaseConfigurationCallModel(
                    level.DeclaringMapperType,
                    first,
                    duplicate,
                    levelOrder));
            }
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<InheritanceCompositionIssueModel>
        FilterInheritedIssues(
            ImmutableArray<InheritanceCompositionIssueModel> issues,
            PairConfigurationConflict propagatedConflicts)
    {
        return issues
            .Where(issue =>
                propagatedConflicts.HasFlag(GetConflict(issue.Kind)))
            .ToImmutableArray();
    }

    private static PairConfigurationConflict GetConflict(
        InheritanceCompositionIssueKind kind)
    {
        return kind switch
        {
            InheritanceCompositionIssueKind.DuplicateIncludeBase =>
                PairConfigurationConflict.DuplicateIncludeBase,
            InheritanceCompositionIssueKind.MissingIncludedPair =>
                PairConfigurationConflict.MissingBasePair,
            InheritanceCompositionIssueKind.IncompatibleSource or
                InheritanceCompositionIssueKind.IncompatibleDestination =>
                PairConfigurationConflict.IncompatibleBasePair,
            InheritanceCompositionIssueKind.InvalidIncludedPair =>
                PairConfigurationConflict.InvalidBasePair,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

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
        if ((model.Conflicts &
             (CompositionConflictMask |
              PairConfigurationConflict.DuplicateResultPolicy |
              PairConfigurationConflict.DuplicateMembers |
              PairConfigurationConflict.DuplicateConvert |
              PairConfigurationConflict.MixedManualAndDeclarative)) != 0)
        {
            return model;
        }

        var failures = ImmutableArray.CreateBuilder<
            InheritedCallbackAccessibilityModel>();

        foreach (var policy in model.Declarative.ResultPolicies)
        {
            AddFailure(
                policy.Kind.ToString(),
                policy.Invocation,
                policy.Expression.DeclaringLevelOrder,
                policy.Expression.InaccessibleReferenceLocations);
        }

        failures.AddRange(FindInaccessibleMemberCallbacks(
            model.Declarative.Members,
            compilation,
            targetMapperType,
            cancellationToken));

        foreach (var conversion in model.Manual.Conversions)
        {
            AddFailure(
                "Convert",
                conversion.Invocation,
                conversion.Expression.DeclaringLevelOrder,
                conversion.Expression.InaccessibleReferenceLocations);
        }

        if (failures.Count == 0)
        {
            return model;
        }

        return model with
        {
            Composition = model.Composition with
            {
                InaccessibleCallbacks = failures.ToImmutable()
            },
            Conflicts = model.Conflicts |
                PairConfigurationConflict.InaccessibleInheritedPlan
        };

        void AddFailure(
            string callbackName,
            InvocationExpressionSyntax invocation,
            int levelOrder,
            ImmutableArray<Location> references)
        {
            if (!references.IsEmpty)
            {
                failures.Add(new InheritedCallbackAccessibilityModel(
                    callbackName,
                    invocation,
                    levelOrder,
                    references));
            }
        }
    }

    private static ImmutableArray<InheritedCallbackAccessibilityModel>
        FindInaccessibleMemberCallbacks(
        ImmutableArray<MembersConfigurationModel> configurations,
        CSharpCompilation compilation,
        INamedTypeSymbol targetMapperType,
        CancellationToken cancellationToken)
    {
        var overriddenNames = new HashSet<string>(StringComparer.Ordinal);
        var failures = ImmutableArray.CreateBuilder<
            InheritedCallbackAccessibilityModel>();

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
                    failures.Add(new InheritedCallbackAccessibilityModel(
                        "Members",
                        configuration.Invocation,
                        configuration.Expression.DeclaringLevelOrder,
                        configuration.Expression
                            .InaccessibleReferenceLocations));
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

                var references = FindInaccessibleReferenceLocations(
                    configuration.Expression.Syntax,
                    configuration.Expression.SemanticModel,
                    compilation,
                    targetMapperType,
                    configuration.Expression.DeclaringMapperType,
                    cancellationToken,
                    overriddenValues);

                if (!references.IsEmpty)
                {
                    failures.Add(new InheritedCallbackAccessibilityModel(
                        "Members",
                        configuration.Invocation,
                        configuration.Expression.DeclaringLevelOrder,
                        references));
                }
            }

            overriddenNames.UnionWith(
                assignments.Select(static assignment =>
                    assignment.MemberName));
        }

        return failures.ToImmutable();
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

    private static MappingPairRegistrationModel FindDeclaredRegistration(
        MapperMappingRegistrationModel registrations,
        InvocationExpressionSyntax invocation)
    {
        foreach (var registration in registrations.Registrations)
        {
            if (registration.Syntax.SyntaxTree == invocation.SyntaxTree &&
                registration.Syntax.Span == invocation.Span)
            {
                return registration;
            }
        }

        throw new InvalidOperationException(
            "The declared mapping registration was not found.");
    }

    private static ImmutableArray<PairConfigurationCandidateModel>
        BuildCandidates(
            MapperMappingPairModel model,
            IEnumerable<PairConfigurationModel> configurations)
    {
        var configurationByKey = configurations.ToDictionary(
            static configuration =>
                MappingPairKey.Create(configuration.Pair));
        var result = ImmutableArray.CreateBuilder<
            PairConfigurationCandidateModel>(
            model.Pairs.Length +
            model.UnsupportedPairs.Length +
            model.UnavailablePairs.Length);

        foreach (var pair in model.Pairs)
        {
            var key = MappingPairKey.Create(pair.SourceType, pair.DestinationType);

            result.Add(new PairConfigurationCandidateModel(
                key,
                configurationByKey[key],
                pair.HasUnifiableConflict));
        }

        foreach (var pair in model.UnsupportedPairs)
        {
            result.Add(new PairConfigurationCandidateModel(
                MappingPairKey.Create(pair.SourceType, pair.DestinationType),
                Configuration: null,
                IsCategory3Invalid: true));
        }

        foreach (var pair in model.UnavailablePairs)
        {
            result.Add(new PairConfigurationCandidateModel(
                MappingPairKey.Create(
                    pair.Registration.SourceType,
                    pair.Registration.DestinationType),
                Configuration: null,
                IsCategory3Invalid: true));
        }

        return result.ToImmutable();
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

        // Unsupported builder flow can still expose a Map registration that
        // deliberately has no linear invocation chain. Keep the registration
        // model so recovery can emit the pair surface, but do not attempt to
        // lower any pair-level configuration after it.
        return new PairConfigurationInvocationChain(ImmutableArray<InvocationExpressionSyntax>.Empty);
    }

    private static PairConfigurationModel BuildPair(
        MappingPairModel pair,
        MappingPairRegistrationModel declaredRegistration,
        PairConfigurationInvocationChain chain,
        SemanticModel semanticModel,
        SemanticModel sourceSemanticModel,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol>
            mapperTypeSubstitutions,
        KnownSymbols knownSymbols,
        CSharpCompilation compilation,
        INamedTypeSymbol targetMapperType,
        CSharpCompilation sourceCompilation,
        INamedTypeSymbol sourceTargetMapperType,
        INamedTypeSymbol declaringMapperType,
        INamedTypeSymbol constructedMapperType,
        int levelOrder,
        CancellationToken cancellationToken)
    {
        var settings = BuildMapMappingMode(
            pair.Registration.Syntax,
            semanticModel,
            cancellationToken);
        var resultPolicies =
            ImmutableArray.CreateBuilder<ResultPolicyConfigurationModel>();
        var members =
            ImmutableArray.CreateBuilder<MembersConfigurationModel>();
        var includeMembers =
            ImmutableArray.CreateBuilder<IncludeMembersConfigurationModel>();
        var conversions =
            ImmutableArray.CreateBuilder<ConvertConfigurationModel>();
        var derivedMappings =
            ImmutableArray.CreateBuilder<DerivedMappingConfigurationModel>();
        var localPlanSlots =
            ImmutableArray.CreateBuilder<MappingPlanSlotOccurrenceModel>();
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
                if (IsPotentialForDerivedInvocation(invocation) &&
                    TryBuildDerivedMappingConfiguration(
                        invocation,
                        sourceSemanticModel,
                        mapperTypeSubstitutions,
                        cancellationToken,
                        hasValidMethodBinding: false,
                        out var invalidDerivedMapping))
                {
                    derivedMappings.Add(invalidDerivedMapping);
                }

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

            if (IsForDerivedMethod(method))
            {
                if (TryBuildDerivedMappingConfiguration(
                        invocation,
                        sourceSemanticModel,
                        mapperTypeSubstitutions,
                        cancellationToken,
                        hasValidMethodBinding: true,
                        out var derivedMapping))
                {
                    derivedMappings.Add(derivedMapping);
                }

                continue;
            }

            if (IsIncludeMembersMethod(method))
            {
                var includeExpression = TryBindConfigurationExpression(
                    invocation,
                    method,
                    semanticModel,
                    compilation,
                    targetMapperType,
                    declaringMapperType,
                    levelOrder,
                    cancellationToken);

                if (includeExpression is not null)
                {
                    localPlanSlots.Add(
                        new MappingPlanSlotOccurrenceModel(
                            invocation,
                            MappingPlanSlotKind.IncludeMembers));
                    includeMembers.Add(
                        new IncludeMembersConfigurationModel(
                            invocation,
                            pair.SourceType,
                            includeExpression));
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
                levelOrder,
                cancellationToken);

            if (expression is null)
            {
                continue;
            }

            switch (method.Name)
            {
                case "Construct":
                    localPlanSlots.Add(new MappingPlanSlotOccurrenceModel(
                        invocation,
                        MappingPlanSlotKind.ResultPolicy));
                    resultPolicies.Add(
                        new ResultPolicyConfigurationModel(
                            invocation,
                            ResultPolicyKind.Construct,
                            expression.DelegateInvokeMethod.Parameters.Length == 1
                                ? ResultPolicyForm.Source
                                : ResultPolicyForm.SourceAndContext,
                            expression));
                    break;

                case "Resolve":
                    localPlanSlots.Add(new MappingPlanSlotOccurrenceModel(
                        invocation,
                        MappingPlanSlotKind.ResultPolicy));
                    resultPolicies.Add(
                        new ResultPolicyConfigurationModel(
                            invocation,
                            ResultPolicyKind.Resolve,
                            expression.DelegateInvokeMethod.Parameters.Length == 2
                                ? ResultPolicyForm.SourceAndPrevious
                                : ResultPolicyForm.SourcePreviousAndContext,
                            expression));
                    break;

                case "ConstructUsing":
                    localPlanSlots.Add(new MappingPlanSlotOccurrenceModel(
                        invocation,
                        MappingPlanSlotKind.ResultPolicy));
                    resultPolicies.Add(
                        new ResultPolicyConfigurationModel(
                            invocation,
                            ResultPolicyKind.ConstructUsing,
                            expression.DelegateInvokeMethod.Parameters.Length == 1
                                ? ResultPolicyForm.Source
                                : ResultPolicyForm.SourceAndContext,
                            expression));
                    break;

                case "ResolveUsing":
                    localPlanSlots.Add(new MappingPlanSlotOccurrenceModel(
                        invocation,
                        MappingPlanSlotKind.ResultPolicy));
                    resultPolicies.Add(
                        new ResultPolicyConfigurationModel(
                            invocation,
                            ResultPolicyKind.ResolveUsing,
                            expression.DelegateInvokeMethod.Parameters.Length == 2
                                ? ResultPolicyForm.SourceAndPrevious
                                : ResultPolicyForm.SourcePreviousAndContext,
                            expression));
                    break;

                case "Members":
                    localPlanSlots.Add(new MappingPlanSlotOccurrenceModel(
                        invocation,
                        MappingPlanSlotKind.Members));
                    members.Add(
                        new MembersConfigurationModel(
                            invocation,
                            expression.DelegateInvokeMethod.Parameters.Length switch
                            {
                                1 => MembersConfigurationForm.Source,
                                2 => MembersConfigurationForm.SourceAndPrevious,
                                3 => MembersConfigurationForm
                                    .SourcePreviousAndResult,
                                _ => MembersConfigurationForm
                                    .SourcePreviousResultAndContext
                            },
                            expression));
                    break;

                case "Convert":
                    localPlanSlots.Add(new MappingPlanSlotOccurrenceModel(
                        invocation,
                        MappingPlanSlotKind.Convert));
                    conversions.Add(
                        new ConvertConfigurationModel(
                            invocation,
                            expression.DelegateInvokeMethod.Parameters.Length switch
                            {
                                1 => ConvertConfigurationForm.Source,
                                2 => ConvertConfigurationForm.SourceAndPrevious,
                                _ => ConvertConfigurationForm
                                    .SourcePreviousAndContext
                            },
                            expression));
                    break;
            }
        }

        var immutableResultPolicies = resultPolicies.ToImmutable();
        var immutableMembers = members.ToImmutable();
        var immutableIncludeMembers = includeMembers.ToImmutable();
        var immutableConversions = conversions.ToImmutable();
        var immutableDerivedMappings = derivedMappings.ToImmutable();
        var conflicts = PairConfigurationConflict.None;

        if (immutableResultPolicies.Length > 1)
        {
            conflicts |= PairConfigurationConflict.DuplicateResultPolicy;
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
            (immutableResultPolicies.Length > 0 ||
             immutableMembers.Length > 0 ||
             immutableIncludeMembers.Length > 0))
        {
            conflicts |= PairConfigurationConflict.MixedManualAndDeclarative;
        }

        var polymorphism = BuildPolymorphicConfiguration(
            pair,
            immutableDerivedMappings,
            GetConfiguredPairType(
                pair.Registration.Syntax,
                sourceSemanticModel,
                mapperTypeSubstitutions,
                sourceCompilation,
                typeArgumentIndex: 0,
                pair.SourceType,
                cancellationToken),
            GetConfiguredPairType(
                pair.Registration.Syntax,
                sourceSemanticModel,
                mapperTypeSubstitutions,
                sourceCompilation,
                typeArgumentIndex: 1,
                pair.DestinationType,
                cancellationToken),
            sourceCompilation,
            sourceTargetMapperType);

        if (polymorphism.Issues.Any(static issue =>
                issue.Kind ==
                    PolymorphicConfigurationIssueKind.DuplicateSource))
        {
            conflicts |= PairConfigurationConflict.DuplicateDerivedMapping;
        }

        if (polymorphism.Issues.Any(static issue =>
                issue.Kind !=
                    PolymorphicConfigurationIssueKind.DuplicateSource))
        {
            conflicts |= PairConfigurationConflict.InvalidDerivedMapping;
        }

        return new PairConfigurationModel(
            pair,
            new PairConfigurationOriginModel(
                declaringMapperType,
                constructedMapperType,
                pair.Registration,
                declaredRegistration,
                levelOrder),
            localPlanSlots.ToImmutable(),
            settings,
            new DeclarativePairConfigurationModel(
                immutableResultPolicies,
                immutableMembers,
                immutableIncludeMembers),
            new ManualPairConfigurationModel(immutableConversions),
            polymorphism,
            new PairConfigurationCompositionModel(
                includeBaseCalls.ToImmutable(),
                ImmutableArray<PairConfigurationSettings>.Empty,
                ImmutableArray<InheritanceCompositionIssueModel>.Empty,
                ImmutableArray<InheritedCallbackAccessibilityModel>.Empty),
            conflicts);
    }

    private static PolymorphicPairConfigurationModel
        BuildPolymorphicConfiguration(
        MappingPairModel pair,
        ImmutableArray<DerivedMappingConfigurationModel> derivedMappings,
        ITypeSymbol baseSourceType,
        ITypeSymbol baseDestinationType,
        CSharpCompilation compilation,
        INamedTypeSymbol targetMapperType)
    {
        if (derivedMappings.IsEmpty)
        {
            return PolymorphicPairConfigurationModel.Empty;
        }

        var issues = ImmutableArray.CreateBuilder<
            PolymorphicConfigurationIssueModel>();
        var firstBySource = new Dictionary<MappingTypeIdentity,
            DerivedMappingConfigurationModel>();

        foreach (var derivedMapping in derivedMappings)
        {
            var sourceIdentity = MappingTypeIdentityPolicy.Create(
                derivedMapping.SourceType);

            if (sourceIdentity == pair.Identity.Source)
            {
                issues.Add(new PolymorphicConfigurationIssueModel(
                    PolymorphicConfigurationIssueKind.SelfLink,
                    derivedMapping));
            }

            if (firstBySource.TryGetValue(
                    sourceIdentity,
                    out var first))
            {
                issues.Add(new PolymorphicConfigurationIssueModel(
                    PolymorphicConfigurationIssueKind.DuplicateSource,
                    derivedMapping,
                    first.Invocation));
            }
            else
            {
                firstBySource.Add(sourceIdentity, derivedMapping);
            }

            if (!derivedMapping.HasValidMethodBinding &&
                !IsDerivedTypeConversion(compilation.ClassifyConversion(
                    derivedMapping.SourceType,
                    baseSourceType)))
            {
                issues.Add(new PolymorphicConfigurationIssueModel(
                    PolymorphicConfigurationIssueKind.IncompatibleSource,
                    derivedMapping));
            }

            if (!derivedMapping.HasValidMethodBinding &&
                !IsDerivedTypeConversion(compilation.ClassifyConversion(
                    derivedMapping.DestinationType,
                    baseDestinationType)))
            {
                issues.Add(new PolymorphicConfigurationIssueModel(
                    PolymorphicConfigurationIssueKind
                        .IncompatibleDestination,
                    derivedMapping));
            }

            if (!IsAccessibleFromGeneratedMapper(
                    derivedMapping.SourceType,
                    compilation,
                    targetMapperType))
            {
                issues.Add(new PolymorphicConfigurationIssueModel(
                    PolymorphicConfigurationIssueKind.InaccessibleSource,
                    derivedMapping));
            }

            if (!IsAccessibleFromGeneratedMapper(
                    derivedMapping.DestinationType,
                    compilation,
                    targetMapperType))
            {
                issues.Add(new PolymorphicConfigurationIssueModel(
                    PolymorphicConfigurationIssueKind
                        .InaccessibleDestination,
                    derivedMapping));
            }
        }

        return new PolymorphicPairConfigurationModel(
            derivedMappings,
            issues.ToImmutable());
    }

    private static bool IsAccessibleFromGeneratedMapper(
        ITypeSymbol type,
        CSharpCompilation compilation,
        INamedTypeSymbol targetMapperType)
    {
        return MappingTypeEligibilityPolicy.GetNameability(
                   type,
                   compilation) == MappingTypeNameability.Available &&
               compilation.IsSymbolAccessibleWithin(
                   type,
                   targetMapperType);
    }

    private static ITypeSymbol GetConfiguredPairType(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol>
            mapperTypeSubstitutions,
        Compilation compilation,
        int typeArgumentIndex,
        ITypeSymbol fallback,
        CancellationToken cancellationToken)
    {
        if (semanticModel.GetSymbolInfo(
                invocation,
                cancellationToken).Symbol is not IMethodSymbol
                {
                    TypeArguments.Length: 2
                } method)
        {
            return fallback;
        }

        return MapperTypeSubstitution.Substitute(
            method.TypeArguments[typeArgumentIndex],
            mapperTypeSubstitutions,
            compilation);
    }

    private static bool IsDerivedTypeConversion(Conversion conversion)
    {
        return conversion.IsIdentity ||
               conversion.IsImplicit &&
               (conversion.IsReference || conversion.IsBoxing ||
                conversion.IsNullable);
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

    private static bool TryBuildDerivedMappingConfiguration(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol>
            mapperTypeSubstitutions,
        CancellationToken cancellationToken,
        bool hasValidMethodBinding,
        out DerivedMappingConfigurationModel configuration)
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

        configuration = new DerivedMappingConfigurationModel(
            invocation,
            MapperTypeSubstitution.Substitute(
                sourceType,
                mapperTypeSubstitutions,
                semanticModel.Compilation),
            MapperTypeSubstitution.Substitute(
                destinationType,
                mapperTypeSubstitutions,
                semanticModel.Compilation),
            hasValidMethodBinding);
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

            case "UnknownDerivedTypeHandling":
                settings = settings with
                {
                    UnknownDerivedTypeHandling =
                        BuildSetting<UnknownDerivedTypeHandlingValue>(
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

            case "Flattening":
                settings = settings with
                {
                    Flattening =
                        BuildSetting<FlatteningValue>(
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
        int declaringLevelOrder,
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
            declaringLevelOrder,
            FindInaccessibleReferenceLocations(
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

    private static bool IsForDerivedMethod(IMethodSymbol method)
    {
        return method.Name == "ForDerived" &&
               method.MethodKind == MethodKind.Ordinary &&
               !method.IsStatic &&
               method.Parameters.Length == 0 &&
               method.TypeArguments.Length == 2 &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       method.ContainingType.OriginalDefinition),
                   MetadataNames.PairMapperBuilder);
    }

    private static bool IsPotentialForDerivedInvocation(
        InvocationExpressionSyntax invocation)
    {
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: var memberName } =>
                memberName,
            SimpleNameSyntax simpleName => simpleName,
            _ => null
        };

        return name is GenericNameSyntax genericName &&
               genericName.Identifier.ValueText == "ForDerived" &&
               genericName.TypeArgumentList.Arguments.Count == 2 &&
               invocation.ArgumentList.Arguments.Count == 0;
    }

    private static bool IsIncludeMembersMethod(IMethodSymbol method)
    {
        return method.Name == "IncludeMembers" &&
               method.MethodKind == MethodKind.Ordinary &&
               !method.IsStatic &&
               method.Parameters.Length == 1 &&
               method.TypeArguments.Length == 0 &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       method.ContainingType.OriginalDefinition),
                   MetadataNames.PairMapperBuilder);
    }

    private static ImmutableArray<Location>
        FindInaccessibleReferenceLocations(
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
            return ImmutableArray<Location>.Empty;
        }

        bool DescendInto(SyntaxNode node) =>
            excludedSubtrees is null ||
            !excludedSubtrees.Contains(node);

        var nodes = expression.DescendantNodesAndSelf(DescendInto)
            .Where(node =>
                excludedSubtrees is null ||
                !excludedSubtrees.Contains(node))
            .ToImmutableArray();
        var locations = ImmutableArray.CreateBuilder<Location>();

        foreach (var baseExpression in nodes.OfType<BaseExpressionSyntax>())
        {
            locations.Add(baseExpression.GetLocation());
        }

        foreach (var name in nodes.OfType<SimpleNameSyntax>())
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
                locations.Add(name.GetLocation());
            }
        }

        return locations
            .OrderBy(static location => location.SourceSpan.Start)
            .GroupBy(static location =>
                (location.SourceTree,
                    location.SourceSpan.Start,
                    location.SourceSpan.Length))
            .Select(static group => group.First())
            .ToImmutableArray();
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

        return method.Name is
                   "Construct" or
                   "Resolve" or
                   "ConstructUsing" or
                   "ResolveUsing" or
                   "Members" or
                   "Convert" &&
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
                   "UnknownDerivedTypeHandling" or
                   "ConstructorSelection" or
                   "MemberSelection" or
                   "Flattening" or
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
            (numericValue & ~GetAtomicMappingModeMask()) != 0)
        {
            value = default;
            return false;
        }

        value = (MappingModeValue)numericValue;
        return true;
    }

    private static int GetAtomicMappingModeMask()
    {
        var mask = 0;

        foreach (var value in Enum.GetValues(typeof(MappingModeValue)))
        {
            var numericValue = (int)value;

            if (numericValue != 0 &&
                (numericValue & (numericValue - 1)) == 0)
            {
                mask |= numericValue;
            }
        }

        return mask;
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
        INamedTypeSymbol DeclaringMapperType,
        PairConfigurationSettings RootSettings,
        ImmutableArray<PairConfigurationModel> Pairs,
        ImmutableArray<PairConfigurationCandidateModel> Candidates,
        ImmutableArray<InvocationExpressionSyntax> BaseConfigureCalls);

    private readonly record struct PairConfigurationCandidateModel(
        MappingPairKey Key,
        PairConfigurationModel? Configuration,
        bool IsCategory3Invalid);

    private enum IncludeBaseLookupStatus
    {
        None,
        Missing,
        Unknown,
        Invalid,
        Found
    }

    private readonly record struct IncludeBaseLookupResult(
        IncludeBaseLookupStatus Status,
        PairConfigurationModel? Configuration)
    {
        public static IncludeBaseLookupResult None =>
            new(IncludeBaseLookupStatus.None, null);

        public static IncludeBaseLookupResult Missing =>
            new(IncludeBaseLookupStatus.Missing, null);

        public static IncludeBaseLookupResult Unknown =>
            new(IncludeBaseLookupStatus.Unknown, null);

        public static IncludeBaseLookupResult Invalid =>
            new(IncludeBaseLookupStatus.Invalid, null);

        public static IncludeBaseLookupResult Found(
            PairConfigurationModel? configuration) =>
            new(IncludeBaseLookupStatus.Found, configuration);
    }

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
