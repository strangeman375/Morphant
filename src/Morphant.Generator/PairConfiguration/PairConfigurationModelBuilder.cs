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
            discovery.HasUnavailableBaseConfiguration);
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
        bool hasUnavailableBaseConfiguration)
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

            foreach (var localPair in level.Pairs)
            {
                var key = MappingPairKey.Create(localPair.Pair);
                effectivePairs.TryGetValue(key, out var basePair);
                var hasBasePair = effectivePairs.ContainsKey(key);
                var composed = ComposePair(
                    localPair,
                    hasBasePair ? basePair : null,
                    hasConnectedBaseConfiguration:
                        levelIndex + 1 < levels.Length,
                    hasUnavailableBaseConfiguration &&
                        levelIndex + 1 == levels.Length);

                effectivePairs[key] = composed;
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
                        configuration with { Pair = pair }));
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
        bool hasUnavailableBaseConfiguration)
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
        var localHasDeclarative =
            !local.Declarative.Constructs.IsEmpty ||
            !local.Declarative.Members.IsEmpty;
        var inheritedHasConvert =
            !inherited.Manual.Conversions.IsEmpty;
        DeclarativePairConfigurationModel declarative;
        ManualPairConfigurationModel manual;

        if (localHasConvert)
        {
            declarative = local.Declarative;
            manual = local.Manual;
            conflicts |= inherited.Conflicts &
                CompositionConflictMask;
        }
        else if (inheritedHasConvert)
        {
            declarative = local.Declarative;
            manual = inherited.Manual;
            conflicts |= inherited.Conflicts;

            if (localHasDeclarative)
            {
                conflicts |=
                    PairConfigurationConflict.MixedManualAndDeclarative;
            }
        }
        else
        {
            declarative = new DeclarativePairConfigurationModel(
                local.Declarative.Constructs.IsEmpty
                    ? inherited.Declarative.Constructs
                    : local.Declarative.Constructs,
                inherited.Declarative.Members.AddRange(
                    local.Declarative.Members));
            manual = local.Manual;
            conflicts |= local.Declarative.Constructs.IsEmpty
                ? inherited.Conflicts
                : inherited.Conflicts &
                    ~PairConfigurationConflict.DuplicateConstruct;
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
            Manual = manual,
            Composition = composition,
            Conflicts = conflicts
        };
    }

    private const PairConfigurationConflict CompositionConflictMask =
        PairConfigurationConflict.DuplicateIncludeBase |
        PairConfigurationConflict.MissingBaseConfiguration |
        PairConfigurationConflict.MissingBasePair;

    private static PairConfigurationModel AddAccessibilityConflict(
        PairConfigurationModel model)
    {
        var accessible = model.Declarative.Constructs.All(static construct =>
                construct.Expression.IsAccessibleFromTargetMapper) &&
            model.Declarative.Members.All(static members =>
                members.Expression.IsAccessibleFromTargetMapper) &&
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
            ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();
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
                includeBaseCalls.Add(invocation);
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
               method.TypeArguments.Length == 0 &&
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
        CancellationToken cancellationToken)
    {
        if (SymbolEqualityComparer.Default.Equals(
                targetMapperType,
                declaringMapperType))
        {
            return true;
        }

        if (expression.DescendantNodesAndSelf()
            .Any(static node => node is BaseExpressionSyntax))
        {
            return false;
        }

        foreach (var name in expression.DescendantNodesAndSelf()
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
            new(
                pair.Identity.Source.Key,
                pair.Identity.Destination.Key);
    }
}
