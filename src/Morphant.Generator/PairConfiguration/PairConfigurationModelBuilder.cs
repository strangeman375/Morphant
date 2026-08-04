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
                PairConfigurationSettings.Empty,
                []);
        }

        var augmentedCompilation = BuildAugmentedCompilation(
            compilation,
            mappingPairs,
            cancellationToken);
        var knownSymbols = KnownSymbols.TryCreate(augmentedCompilation);

        if (knownSymbols is null)
        {
            return new MapperPairConfigurationModel(
                mappingPairs,
                PairConfigurationSettings.Empty,
                []);
        }

        var semanticModel = augmentedCompilation.GetSemanticModel(
            discovery.ConfigureInfo.Syntax.SyntaxTree);
        var rootSettings = BuildRootSettings(
            discovery.InvocationChains,
            semanticModel,
            knownSymbols,
            cancellationToken);
        var pairModels = ImmutableArray.CreateBuilder<PairConfigurationModel>(
            mappingPairs.Pairs.Length);

        foreach (var pair in mappingPairs.Pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chain = FindRegistrationChain(
                discovery.InvocationChains,
                pair.Registration.Syntax);

            pairModels.Add(
                BuildPair(
                    pair,
                    chain,
                    semanticModel,
                    knownSymbols,
                    cancellationToken));
        }

        return new MapperPairConfigurationModel(
            mappingPairs,
            rootSettings,
            pairModels.ToImmutable());
    }

    private static CSharpCompilation BuildAugmentedCompilation(
        CSharpCompilation compilation,
        MapperMappingPairModel mappingPairs,
        CancellationToken cancellationToken)
    {
        var mapperModels = ImmutableArray.Create(mappingPairs);
        var constructionRequests =
            ConstructionSurfacePipeline.BuildRequests(
                mapperModels,
                compilation,
                cancellationToken);
        var memberRequests = MemberSurfacePipeline.BuildRequests(
            mapperModels,
            compilation,
            cancellationToken);
        var parseOptions = mappingPairs.ConfigureSyntax.SyntaxTree.Options as
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

            if (!IsGeneratedConfigurationMethod(method))
            {
                continue;
            }

            var expression = TryBindConfigurationExpression(
                invocation,
                method,
                semanticModel,
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
            PairConfigurationCompositionModel.Empty,
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
            delegateInvokeMethod);
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
}
