using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.TypeMapperGeneration;

namespace Morphant.Generator.Settings;

internal static class MappingSettingsDiagnosticPipeline
{
    private const string ManualModelName = "a Convert mapping";

    private const string MissingConstructionModelName =
        "this destination type";

    private const PairConfigurationConflict LocalModelConflict =
        PairConfigurationConflict.MixedManualAndDeclarative;

    private const PairConfigurationConflict CompositionConflict =
        PairConfigurationConflict.DuplicateIncludeBase |
        PairConfigurationConflict.MissingBaseConfiguration |
        PairConfigurationConflict.MissingBasePair |
        PairConfigurationConflict.IncompatibleBasePair |
        PairConfigurationConflict.InvalidBasePair |
        PairConfigurationConflict.InaccessibleInheritedPlan |
        PairConfigurationConflict.DuplicateDerivedMapping |
        PairConfigurationConflict.InvalidDerivedMapping;

    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<MappingSettings> assemblySettings,
        IncrementalValueProvider<ImmutableArray<MapperContractAnalysis>>
            contractAnalyses)
    {
        var diagnostics = GeneratorStageGuard.Select(
            context,
            contractAnalyses.Combine(assemblySettings),
            "BuildMappingSettingsDiagnostics",
            static (source, cancellationToken) =>
                BuildDiagnostics(
                    source.Left,
                    source.Right,
                    cancellationToken),
            ImmutableArray<Diagnostic>.Empty);

        DiagnosticPipeline.Register(
            context,
            diagnostics,
            "MappingSettingsDiagnostics");
    }

    private static ImmutableArray<Diagnostic> BuildDiagnostics(
        ImmutableArray<MapperContractAnalysis> analyses,
        MappingSettings assemblySettings,
        CancellationToken cancellationToken)
    {
        var cSharpOrigins = new Dictionary<CSharpOriginKey,
            CSharpOriginCandidate>();
        var msBuildOrigins = new HashSet<MappingSettingKind>();
        var inapplicable =
            ImmutableArray.CreateBuilder<InapplicableCandidate>();
        var seenMappers = new HashSet<ISymbol>(
            SymbolEqualityComparer.Default);
        foreach (var analysis in analyses)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var configuration = analysis.Configuration;
            var declaration = configuration.Declaration;

            if (!declaration.CanGenerateExecutableArtifact ||
                configuration.HasMapperWideConfigurationFlowFailure ||
                !seenMappers.Add(declaration.MapperType))
            {
                continue;
            }

            foreach (var pair in configuration.Pairs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (analysis.Excludes(pair.Pair.Identity) ||
                    HasMappingFlowFailure(configuration, pair.Pair.Identity))
                {
                    continue;
                }

                AnalyzePair(
                    configuration,
                    pair,
                    declaration,
                    assemblySettings,
                    new SyntaxTreeOrdering(
                        declaration.Compilation.SyntaxTrees),
                    cSharpOrigins,
                    msBuildOrigins,
                    inapplicable,
                    cancellationToken);
            }
        }

        var result = ImmutableArray.CreateBuilder<Diagnostic>();

        foreach (var candidate in cSharpOrigins.Values
                     .OrderBy(static candidate => candidate.TreeOrder)
                     .ThenBy(static candidate => candidate.Position)
                     .ThenBy(static candidate => candidate.SettingName,
                         StringComparer.Ordinal))
        {
            result.Add(Diagnostic.Create(
                MappingSettingsDiagnosticDescriptors.InvalidSettingValue,
                candidate.Location,
                candidate.SettingName));
        }

        foreach (var kind in msBuildOrigins
                     .OrderBy(static kind => PropertyName(kind),
                         StringComparer.Ordinal))
        {
            result.Add(Diagnostic.Create(
                MappingSettingsDiagnosticDescriptors
                    .InvalidMsBuildSettingValue,
                Location.None,
                PropertyName(kind),
                InvalidPropertyValue(assemblySettings, kind)));
        }

        foreach (var candidate in inapplicable
                     .OrderBy(static candidate => candidate.MapperIdentity,
                         StringComparer.Ordinal)
                     .ThenBy(static candidate => candidate.PairKey,
                         StringComparer.Ordinal)
                     .ThenBy(static candidate => candidate.SettingName,
                         StringComparer.Ordinal)
                     .ThenBy(static candidate => candidate.Position))
        {
            result.Add(Diagnostic.Create(
                MappingSettingsDiagnosticDescriptors.InapplicableSetting,
                candidate.Location,
                [candidate.AdditionalLocation],
                properties: null,
                candidate.SettingName,
                candidate.ModelName,
                candidate.Contract,
                candidate.MapperDisplayName));
        }

        return result.ToImmutable();
    }

    private static void AnalyzePair(
        MapperPairConfigurationModel mapper,
        PairConfigurationModel pair,
        MapperDeclarationInfo declaration,
        MappingSettings assemblySettings,
        SyntaxTreeOrdering syntaxTreeOrder,
        IDictionary<CSharpOriginKey, CSharpOriginCandidate> cSharpOrigins,
        ISet<MappingSettingKind> msBuildOrigins,
        ImmutableArray<InapplicableCandidate>.Builder inapplicable,
        CancellationToken cancellationToken)
    {
        var localConvert = FindFirstLocalSlot(
            pair,
            MappingPlanSlotKind.Convert);
        var hasMixedModel = pair.Conflicts.HasFlag(LocalModelConflict);
        var inapplicableKinds = new HashSet<MappingSettingKind>();

        if (!hasMixedModel && localConvert is { } convert)
        {
            AddManualInapplicableSettings(
                pair,
                declaration,
                convert.Invocation,
                inapplicableKinds,
                inapplicable);
        }
        else if (!hasMixedModel &&
                 (pair.Pair.Capabilities.DirectConstruction ||
                  pair.Pair.Capabilities.IntrinsicConstruction))
        {
            AddMissingConstructionInapplicableSetting(
                pair,
                declaration,
                inapplicableKinds,
                inapplicable);
        }

        var compositionReliable =
            (pair.Conflicts & CompositionConflict) == 0;
        var mappingMode = Resolve(
            pair,
            mapper,
            assemblySettings.MappingMode,
            MappingModeValue.CreateAndUpdate,
            static settings => settings.MappingMode,
            compositionReliable);

        AddInvalidOrigin(
            MappingSettingKind.MappingMode,
            mappingMode,
            syntaxTreeOrder,
            cSharpOrigins,
            msBuildOrigins);

        if (!mappingMode.TryGetValue(out var mode))
        {
            return;
        }

        var unknownDerived = Resolve(
            pair,
            mapper,
            assemblySettings.UnknownDerivedTypeHandling,
            UnknownDerivedTypeHandlingValue.UseBaseMapping,
            static settings => settings.UnknownDerivedTypeHandling,
            compositionReliable);

        AddInvalidOrigin(
            MappingSettingKind.UnknownDerivedTypeHandling,
            unknownDerived,
            syntaxTreeOrder,
            cSharpOrigins,
            msBuildOrigins);

        var hasLocalDeclarativeSlot = pair.LocalPlanSlots.Any(
            static occurrence => occurrence.Kind is
                MappingPlanSlotKind.ResultPolicy or
                MappingPlanSlotKind.Members);
        var manual = !hasMixedModel &&
                     (localConvert is not null ||
                      !hasLocalDeclarativeSlot &&
                      !pair.Manual.Conversions.IsEmpty);

        if (manual || hasMixedModel)
        {
            return;
        }

        var hasEnabledOperation =
            Supports(mode, MappingModeValue.Create) ||
            Supports(mode, MappingModeValue.Update);
        var nullSource = Resolve(
            pair,
            mapper,
            assemblySettings.NullSourceHandling,
            NullSourceHandlingValue.ReturnNull,
            static settings => settings.NullSourceHandling,
            compositionReliable);
        var nullDestination = Resolve(
            pair,
            mapper,
            assemblySettings.NullDestinationHandling,
            NullDestinationHandlingValue.Create,
            static settings => settings.NullDestinationHandling,
            compositionReliable);
        var memberSelection = Resolve(
            pair,
            mapper,
            assemblySettings.MemberSelection,
            MemberSelectionValue.Auto,
            static settings => settings.MemberSelection,
            compositionReliable);
        var flattening = Resolve(
            pair,
            mapper,
            assemblySettings.Flattening,
            FlatteningValue.Auto,
            static settings => settings.Flattening,
            compositionReliable);
        var constructorSelection = Resolve(
            pair,
            mapper,
            assemblySettings.ConstructorSelection,
            ConstructorSelectionValue.Unambiguous,
            static settings => settings.ConstructorSelection,
            compositionReliable);
        var unmappedValidation = Resolve(
            pair,
            mapper,
            assemblySettings.UnmappedMemberValidation,
            UnmappedMemberValidationValue.None,
            static settings => settings.UnmappedMemberValidation,
            compositionReliable);

        if (hasEnabledOperation)
        {
            AddInvalidUnlessInapplicable(
                MappingSettingKind.NullSourceHandling,
                nullSource,
                inapplicableKinds,
                syntaxTreeOrder,
                cSharpOrigins,
                msBuildOrigins);
            AddInvalidUnlessInapplicable(
                MappingSettingKind.MemberSelection,
                memberSelection,
                inapplicableKinds,
                syntaxTreeOrder,
                cSharpOrigins,
                msBuildOrigins);
            AddInvalidUnlessInapplicable(
                MappingSettingKind.Flattening,
                flattening,
                inapplicableKinds,
                syntaxTreeOrder,
                cSharpOrigins,
                msBuildOrigins);
            AddInvalidUnlessInapplicable(
                MappingSettingKind.UnmappedMemberValidation,
                unmappedValidation,
                inapplicableKinds,
                syntaxTreeOrder,
                cSharpOrigins,
                msBuildOrigins);
        }

        if (Supports(mode, MappingModeValue.Update))
        {
            AddInvalidUnlessInapplicable(
                MappingSettingKind.NullDestinationHandling,
                nullDestination,
                inapplicableKinds,
                syntaxTreeOrder,
                cSharpOrigins,
                msBuildOrigins);
        }

        if (!inapplicableKinds.Contains(
                MappingSettingKind.ConstructorSelection) &&
            HasReachableConventionPath(
                pair,
                mode,
                nullDestination,
                cancellationToken))
        {
            AddInvalidOrigin(
                MappingSettingKind.ConstructorSelection,
                constructorSelection,
                syntaxTreeOrder,
                cSharpOrigins,
                msBuildOrigins);
        }
    }

    private static void AddManualInapplicableSettings(
        PairConfigurationModel pair,
        MapperDeclarationInfo declaration,
        InvocationExpressionSyntax convert,
        ISet<MappingSettingKind> inapplicableKinds,
        ImmutableArray<InapplicableCandidate>.Builder candidates)
    {
        var additionalLocation = GetInvocationNameLocation(convert);

        AddIfExplicit(
            MappingSettingKind.NullSourceHandling,
            pair.Settings.NullSourceHandling);
        AddIfExplicit(
            MappingSettingKind.NullDestinationHandling,
            pair.Settings.NullDestinationHandling);
        AddIfExplicit(
            MappingSettingKind.ConstructorSelection,
            pair.Settings.ConstructorSelection);
        AddIfExplicit(
            MappingSettingKind.MemberSelection,
            pair.Settings.MemberSelection);
        AddIfExplicit(
            MappingSettingKind.Flattening,
            pair.Settings.Flattening);
        AddIfExplicit(
            MappingSettingKind.UnmappedMemberValidation,
            pair.Settings.UnmappedMemberValidation);

        void AddIfExplicit<TValue>(
            MappingSettingKind kind,
            PairConfigurationSetting<TValue> setting)
            where TValue : struct, Enum
        {
            if (setting.Origin != PairConfigurationSettingOrigin.Explicit ||
                setting.Syntax is not InvocationExpressionSyntax invocation)
            {
                return;
            }

            inapplicableKinds.Add(kind);
            candidates.Add(CreateInapplicableCandidate(
                kind,
                invocation,
                ManualModelName,
                additionalLocation,
                pair,
                declaration));
        }
    }

    private static void AddMissingConstructionInapplicableSetting(
        PairConfigurationModel pair,
        MapperDeclarationInfo declaration,
        ISet<MappingSettingKind> inapplicableKinds,
        ImmutableArray<InapplicableCandidate>.Builder candidates)
    {
        var setting = pair.Settings.ConstructorSelection;

        if (setting.Origin != PairConfigurationSettingOrigin.Explicit ||
            setting.Syntax is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        inapplicableKinds.Add(MappingSettingKind.ConstructorSelection);
        candidates.Add(CreateInapplicableCandidate(
            MappingSettingKind.ConstructorSelection,
            invocation,
            MissingConstructionModelName,
            GetDestinationTypeArgumentLocation(
                pair.Pair.Registration.Syntax),
            pair,
            declaration));
    }

    private static InapplicableCandidate CreateInapplicableCandidate(
        MappingSettingKind kind,
        InvocationExpressionSyntax invocation,
        string modelName,
        Location additionalLocation,
        PairConfigurationModel pair,
        MapperDeclarationInfo declaration)
    {
        var location = GetInvocationNameLocation(invocation);

        return new InapplicableCandidate(
            declaration.MapperIdentity,
            PairKey(pair.Pair.Identity),
            SettingName(kind),
            location.SourceSpan.Start,
            location,
            additionalLocation,
            modelName,
            MapperContractDisplay.Create(
                pair.Pair.SourceType,
                pair.Pair.DestinationType),
            declaration.MapperDisplayName);
    }

    private static ResolvedSetting<TValue> Resolve<TValue>(
        PairConfigurationModel pair,
        MapperPairConfigurationModel mapper,
        TValue? assemblyValue,
        TValue libraryDefault,
        Func<PairConfigurationSettings, PairConfigurationSetting<TValue>>
            selector,
        bool compositionReliable)
        where TValue : struct, Enum
    {
        var local = ResolveLevel(selector(pair.Settings));

        if (local.Status != ResolvedSettingStatus.Continue)
        {
            return local;
        }

        if (!compositionReliable)
        {
            return ResolvedSetting<TValue>.Unknown;
        }

        foreach (var settings in pair.Composition.IncludedBaseSettings)
        {
            var included = ResolveLevel(selector(settings));

            if (included.Status != ResolvedSettingStatus.Continue)
            {
                return included;
            }
        }

        var root = ResolveLevel(selector(mapper.RootSettings));

        if (root.Status != ResolvedSettingStatus.Continue)
        {
            return root;
        }

        foreach (var settings in mapper.BaseRootSettings)
        {
            var baseRoot = ResolveLevel(selector(settings));

            if (baseRoot.Status != ResolvedSettingStatus.Continue)
            {
                return baseRoot;
            }
        }

        if (assemblyValue is not { } value)
        {
            return ResolvedSetting<TValue>.InvalidAssembly;
        }

        return IsDefault(value)
            ? ResolvedSetting<TValue>.Valid(libraryDefault)
            : ResolvedSetting<TValue>.Valid(value);
    }

    private static ResolvedSetting<TValue> ResolveLevel<TValue>(
        PairConfigurationSetting<TValue> setting)
        where TValue : struct, Enum
    {
        if (setting.Origin == PairConfigurationSettingOrigin.Unset)
        {
            return ResolvedSetting<TValue>.Continue;
        }

        if (setting.Value is not { } value)
        {
            return setting.Syntax is { } syntax
                ? ResolvedSetting<TValue>.InvalidCSharp(syntax)
                : ResolvedSetting<TValue>.Unknown;
        }

        return IsDefault(value)
            ? ResolvedSetting<TValue>.Continue
            : ResolvedSetting<TValue>.Valid(value);
    }

    private static void AddInvalidUnlessInapplicable<TValue>(
        MappingSettingKind kind,
        ResolvedSetting<TValue> setting,
        ISet<MappingSettingKind> inapplicableKinds,
        SyntaxTreeOrdering syntaxTreeOrder,
        IDictionary<CSharpOriginKey, CSharpOriginCandidate> cSharpOrigins,
        ISet<MappingSettingKind> msBuildOrigins)
        where TValue : struct, Enum
    {
        if (!inapplicableKinds.Contains(kind))
        {
            AddInvalidOrigin(
                kind,
                setting,
                syntaxTreeOrder,
                cSharpOrigins,
                msBuildOrigins);
        }
    }

    private static void AddInvalidOrigin<TValue>(
        MappingSettingKind kind,
        ResolvedSetting<TValue> setting,
        SyntaxTreeOrdering syntaxTreeOrder,
        IDictionary<CSharpOriginKey, CSharpOriginCandidate> cSharpOrigins,
        ISet<MappingSettingKind> msBuildOrigins)
        where TValue : struct, Enum
    {
        if (setting.Status == ResolvedSettingStatus.InvalidAssembly)
        {
            msBuildOrigins.Add(kind);
            return;
        }

        if (setting.Status != ResolvedSettingStatus.InvalidCSharp ||
            setting.Syntax is not InvocationExpressionSyntax invocation ||
            invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression is
                not { } expression)
        {
            return;
        }

        var location = expression.GetLocation();
        var key = new CSharpOriginKey(
            expression.SyntaxTree,
            expression.Span,
            kind);

        if (!cSharpOrigins.ContainsKey(key))
        {
            cSharpOrigins.Add(
                key,
                new CSharpOriginCandidate(
                    syntaxTreeOrder.GetOrderOrDefault(
                        expression.SyntaxTree),
                    location.SourceSpan.Start,
                    SettingName(kind),
                    location));
        }
    }

    private static bool HasReachableConventionPath(
        PairConfigurationModel pair,
        MappingModeValue mode,
        ResolvedSetting<NullDestinationHandlingValue> nullDestination,
        CancellationToken cancellationToken)
    {
        if (!pair.Pair.Capabilities.StructuredConstruction)
        {
            return false;
        }

        var canCreate = Supports(mode, MappingModeValue.Create);
        var canUpdate = Supports(mode, MappingModeValue.Update);
        var canUpdateWithoutPrevious =
            canUpdate &&
            CanBeNull(pair.Pair.DestinationType) &&
            nullDestination.TryGetValue(out var nullDestinationValue) &&
            nullDestinationValue == NullDestinationHandlingValue.Create;
        var resultPolicy = pair.Declarative.ResultPolicies.FirstOrDefault();

        if (resultPolicy == default)
        {
            return canCreate || canUpdateWithoutPrevious;
        }

        if (resultPolicy.Kind is
            ResultPolicyKind.ConstructUsing or
            ResultPolicyKind.ResolveUsing ||
            !ContainsByConvention(resultPolicy, cancellationToken))
        {
            return false;
        }

        return resultPolicy.Kind == ResultPolicyKind.Construct
            ? canCreate || canUpdateWithoutPrevious
            : canCreate || canUpdate;
    }

    private static bool ContainsByConvention(
        ResultPolicyConfigurationModel resultPolicy,
        CancellationToken cancellationToken)
    {
        foreach (var invocation in resultPolicy.Expression.Syntax
                     .DescendantNodesAndSelf()
                     .OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DeclarativeIntrinsic.TryGetKind(
                    invocation,
                    resultPolicy.Expression.SemanticModel,
                    cancellationToken,
                    out var kind,
                    out _) &&
                kind == DeclarativeIntrinsicKind.ByConvention)
            {
                return true;
            }
        }

        return false;
    }

    private static MappingPlanSlotOccurrenceModel? FindFirstLocalSlot(
        PairConfigurationModel pair,
        MappingPlanSlotKind kind)
    {
        foreach (var occurrence in pair.LocalPlanSlots)
        {
            if (occurrence.Kind == kind)
            {
                return occurrence;
            }
        }

        return null;
    }

    private static bool HasMappingFlowFailure(
        MapperPairConfigurationModel configuration,
        MappingPairIdentity identity)
    {
        return configuration.FlowBreaks.Any(flowBreak =>
            flowBreak.Kind == BuilderFlowBreakKind.Mapping &&
            flowBreak.Registration is { } registration &&
            IsIdentity(registration, identity) &&
            !IsDiscardedDuplicate(configuration, registration));
    }

    private static bool IsIdentity(
        MappingPairRegistrationModel registration,
        MappingPairIdentity identity)
    {
        var registrationIdentity = new MappingPairIdentity(
            MappingTypeIdentityPolicy.Create(registration.SourceType),
            MappingTypeIdentityPolicy.Create(registration.DestinationType));

        return StringComparer.Ordinal.Equals(
                   registrationIdentity.Source.Key,
                   identity.Source.Key) &&
               StringComparer.Ordinal.Equals(
                   registrationIdentity.Destination.Key,
                   identity.Destination.Key);
    }

    private static bool IsDiscardedDuplicate(
        MapperPairConfigurationModel configuration,
        MappingPairRegistrationModel registration)
    {
        return configuration.SurfaceMappingPairs.Any(model =>
            model.DuplicateRegistrations.Any(duplicate =>
                duplicate.Registration.Syntax.SyntaxTree ==
                    registration.Syntax.SyntaxTree &&
                duplicate.Registration.Syntax.Span ==
                    registration.Syntax.Span));
    }

    private static bool Supports(
        MappingModeValue value,
        MappingModeValue operation)
    {
        return (value & operation) != 0;
    }

    private static bool CanBeNull(ITypeSymbol type)
    {
        return type.IsReferenceType ||
               type is INamedTypeSymbol named &&
               named.OriginalDefinition.SpecialType ==
                   SpecialType.System_Nullable_T;
    }

    private static bool IsDefault<TValue>(TValue value)
        where TValue : struct, Enum
    {
        return EqualityComparer<TValue>.Default.Equals(value, default);
    }

    private static Location GetInvocationNameLocation(
        InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            SimpleNameSyntax name => name.Identifier.GetLocation(),
            MemberAccessExpressionSyntax memberAccess =>
                memberAccess.Name.Identifier.GetLocation(),
            MemberBindingExpressionSyntax memberBinding =>
                memberBinding.Name.Identifier.GetLocation(),
            _ => invocation.GetLocation()
        };
    }

    private static Location GetDestinationTypeArgumentLocation(
        InvocationExpressionSyntax invocation)
    {
        var name = invocation.Expression
            .DescendantNodesAndSelf()
            .OfType<GenericNameSyntax>()
            .First(candidate =>
                candidate.Identifier.ValueText == "Map" &&
                candidate.TypeArgumentList.Arguments.Count == 2);

        return name.TypeArgumentList.Arguments[1].GetLocation();
    }

    private static string SettingName(MappingSettingKind kind)
    {
        return kind switch
        {
            MappingSettingKind.MappingMode => "MappingMode",
            MappingSettingKind.NullSourceHandling => "NullSourceHandling",
            MappingSettingKind.NullDestinationHandling =>
                "NullDestinationHandling",
            MappingSettingKind.UnknownDerivedTypeHandling =>
                "UnknownDerivedTypeHandling",
            MappingSettingKind.ConstructorSelection =>
                "ConstructorSelection",
            MappingSettingKind.MemberSelection => "MemberSelection",
            MappingSettingKind.Flattening => "Flattening",
            MappingSettingKind.UnmappedMemberValidation =>
                "UnmappedMemberValidation",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private static string PropertyName(MappingSettingKind kind)
    {
        return "Morphant" + SettingName(kind);
    }

    private static string InvalidPropertyValue(
        MappingSettings settings,
        MappingSettingKind kind)
    {
        var values = settings.InvalidMsBuildValues;

        return kind switch
        {
            MappingSettingKind.MappingMode => values.MappingMode,
            MappingSettingKind.NullSourceHandling =>
                values.NullSourceHandling,
            MappingSettingKind.NullDestinationHandling =>
                values.NullDestinationHandling,
            MappingSettingKind.UnknownDerivedTypeHandling =>
                values.UnknownDerivedTypeHandling,
            MappingSettingKind.ConstructorSelection =>
                values.ConstructorSelection,
            MappingSettingKind.MemberSelection => values.MemberSelection,
            MappingSettingKind.Flattening => values.Flattening,
            MappingSettingKind.UnmappedMemberValidation =>
                values.UnmappedMemberValidation,
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        } ?? string.Empty;
    }

    private static string PairKey(MappingPairIdentity identity)
    {
        return identity.Source.Key + "->" + identity.Destination.Key;
    }

    private enum MappingSettingKind
    {
        MappingMode,
        NullSourceHandling,
        NullDestinationHandling,
        UnknownDerivedTypeHandling,
        ConstructorSelection,
        MemberSelection,
        Flattening,
        UnmappedMemberValidation
    }

    private enum ResolvedSettingStatus
    {
        Continue,
        Valid,
        InvalidCSharp,
        InvalidAssembly,
        Unknown
    }

    private readonly record struct ResolvedSetting<TValue>(
        ResolvedSettingStatus Status,
        TValue? Value,
        SyntaxNode? Syntax)
        where TValue : struct, Enum
    {
        public static ResolvedSetting<TValue> Continue =>
            new(ResolvedSettingStatus.Continue, null, null);

        public static ResolvedSetting<TValue> Unknown =>
            new(ResolvedSettingStatus.Unknown, null, null);

        public static ResolvedSetting<TValue> InvalidAssembly =>
            new(ResolvedSettingStatus.InvalidAssembly, null, null);

        public static ResolvedSetting<TValue> Valid(TValue value) =>
            new(ResolvedSettingStatus.Valid, value, null);

        public static ResolvedSetting<TValue> InvalidCSharp(
            SyntaxNode syntax) =>
            new(ResolvedSettingStatus.InvalidCSharp, null, syntax);

        public bool TryGetValue(out TValue value)
        {
            if (Status == ResolvedSettingStatus.Valid &&
                Value is { } resolved)
            {
                value = resolved;
                return true;
            }

            value = default;
            return false;
        }
    }

    private readonly record struct CSharpOriginKey(
        SyntaxTree Tree,
        TextSpan Span,
        MappingSettingKind Kind);

    private readonly record struct CSharpOriginCandidate(
        int TreeOrder,
        int Position,
        string SettingName,
        Location Location);

    private readonly record struct InapplicableCandidate(
        string MapperIdentity,
        string PairKey,
        string SettingName,
        int Position,
        Location Location,
        Location AdditionalLocation,
        string ModelName,
        string Contract,
        string MapperDisplayName);
}
