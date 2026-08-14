using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Morphant.Generator.MappingPair;
using Morphant.Generator.TypeMapperConfigure;

namespace Morphant.Generator.PairConfiguration;

internal static class PairConfigurationDiscoveryPipeline
{
    public static IncrementalValuesProvider<PairConfigurationDiscoveryModel>
        Build(
            IncrementalValueProvider<CompilationContext> compilationContext,
            IncrementalValuesProvider<TypeMapperConfigureInfo> configureInfos)
    {
        return configureInfos
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
                TryBuild(source, cancellationToken))
            .WhereHasValue()
            .WithTrackingName(
                MorphantGeneratorStageNames
                    .BuildPairConfigurationDiscoveryModels);
    }

    private static PairConfigurationDiscoveryModel? TryBuild(
        (
            TypeMapperConfigureInfo ConfigureInfo,
            CompilationContext Context
        ) source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var (configureInfo, context) = source;

        if (context.KnownSymbols is not { } knownSymbols)
        {
            return null;
        }

        var levels =
            ImmutableArray.CreateBuilder<PairConfigurationDiscoveryLevel>();
        var unavailableBaseConfigurations = ImmutableArray.CreateBuilder<
            UnavailableBaseConfigurationModel>();
        var flowBreaks =
            ImmutableArray.CreateBuilder<BuilderFlowBreakModel>();
        var currentInfo = configureInfo;
        var currentConstructedType = configureInfo.MapperType;
        var hasInvalidBaseConfiguration = false;
        var visitedMethods = new HashSet<IMethodSymbol>(
            SymbolEqualityComparer.Default);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryBuildLevel(
                    currentInfo,
                    currentConstructedType,
                    context,
                    knownSymbols,
                    cancellationToken,
                    out var level))
            {
                return null;
            }

            var levelOrder = levels.Count;
            level = level with
            {
                FlowBreaks = level.FlowBreaks
                    .Select(flowBreak => flowBreak with
                    {
                        LevelOrder = levelOrder
                    })
                    .ToImmutableArray()
            };
            levels.Add(level);
            flowBreaks.AddRange(level.FlowBreaks);

            if (level.BaseConfigureCalls.IsEmpty)
            {
                break;
            }

            if (!TryResolveConnectedBaseConfigure(
                    level,
                    context.Compilation,
                    cancellationToken,
                    out var baseMethod,
                    out var constructedBaseType))
            {
                hasInvalidBaseConfiguration = true;
                break;
            }

            if (!TryGetSourceConfigureInfo(
                    baseMethod,
                    context,
                    cancellationToken,
                    out var baseInfo))
            {
                unavailableBaseConfigurations.Add(
                    new UnavailableBaseConfigurationModel(
                        level.BaseConfigureCalls[0],
                        constructedBaseType,
                        levelOrder));
                hasInvalidBaseConfiguration = true;
                break;
            }

            if (!visitedMethods.Add(baseMethod.OriginalDefinition))
            {
                hasInvalidBaseConfiguration = true;
                break;
            }

            currentInfo = baseInfo;
            currentConstructedType = constructedBaseType;
        }

        return new PairConfigurationDiscoveryModel(
            configureInfo,
            levels[0].InstantiatedRegistrations,
            levels.ToImmutable(),
            unavailableBaseConfigurations.ToImmutable(),
            flowBreaks.ToImmutable(),
            hasInvalidBaseConfiguration);
    }

    private static bool TryBuildLevel(
        TypeMapperConfigureInfo configureInfo,
        INamedTypeSymbol constructedMapperType,
        CompilationContext context,
        KnownSymbols knownSymbols,
        CancellationToken cancellationToken,
        out PairConfigurationDiscoveryLevel level)
    {
        if (configureInfo.Syntax.ParameterList.Parameters.Count != 1)
        {
            level = default;
            return false;
        }

        var semanticModel = context.Compilation.GetSemanticModel(
            configureInfo.Syntax.SyntaxTree);
        var builderParameterSyntax =
            configureInfo.Syntax.ParameterList.Parameters[0];

        if (semanticModel.GetDeclaredSymbol(
                builderParameterSyntax,
                cancellationToken) is not IParameterSymbol builderParameter)
        {
            level = default;
            return false;
        }

        var flowAnalysis = BuilderFlowAnalyzer.Build(
            configureInfo.Syntax,
            semanticModel,
            builderParameter,
            knownSymbols,
            cancellationToken);
        var substitutions = MapperTypeSubstitution.Build(
            configureInfo.MapperType,
            constructedMapperType);
        var instantiatedRegistrations = flowAnalysis.Registrations
            .Select(registration =>
                registration with
                {
                    SourceType = MapperTypeSubstitution.Substitute(
                        registration.SourceType,
                        substitutions,
                        context.Compilation),
                    DestinationType = MapperTypeSubstitution.Substitute(
                        registration.DestinationType,
                        substitutions,
                        context.Compilation)
                })
            .ToImmutableArray();
        var instantiatedByInvocation = instantiatedRegistrations
            .ToDictionary(
                static registration =>
                    (registration.Syntax.SyntaxTree,
                        registration.Syntax.SpanStart,
                        registration.Syntax.Span.Length));
        var instantiatedBreaks = flowAnalysis.FlowBreaks
            .Select(flowBreak => flowBreak.Registration is { } registration &&
                instantiatedByInvocation.TryGetValue(
                    (registration.Syntax.SyntaxTree,
                        registration.Syntax.SpanStart,
                        registration.Syntax.Span.Length),
                    out var instantiated)
                    ? flowBreak with { Registration = instantiated }
                    : flowBreak)
            .ToImmutableArray();

        level = new PairConfigurationDiscoveryLevel(
            configureInfo,
            constructedMapperType,
            new MapperMappingRegistrationModel(
                configureInfo.Syntax,
                flowAnalysis.Registrations),
            new MapperMappingRegistrationModel(
                configureInfo.Syntax,
                instantiatedRegistrations),
            flowAnalysis.InvocationChains,
            flowAnalysis.BaseConfigureCalls,
            instantiatedBreaks);
        return true;
    }

    private static bool TryResolveConnectedBaseConfigure(
        PairConfigurationDiscoveryLevel level,
        Compilation compilation,
        CancellationToken cancellationToken,
        out IMethodSymbol method,
        out INamedTypeSymbol constructedBaseType)
    {
        var semanticModel = compilation.GetSemanticModel(
            level.ConfigureInfo.Syntax.SyntaxTree);

        if (semanticModel.GetSymbolInfo(
                level.BaseConfigureCalls[0],
                cancellationToken).Symbol is not IMethodSymbol resolvedMethod)
        {
            method = null!;
            constructedBaseType = null!;
            return false;
        }

        var resolvedConstructedBaseType = FindConstructedBaseType(
            level.ConstructedMapperType,
            resolvedMethod.ContainingType);

        if (resolvedConstructedBaseType is null)
        {
            method = null!;
            constructedBaseType = null!;
            return false;
        }

        method = resolvedMethod;
        constructedBaseType = resolvedConstructedBaseType;
        return true;
    }

    private static bool TryGetSourceConfigureInfo(
        IMethodSymbol method,
        CompilationContext context,
        CancellationToken cancellationToken,
        out TypeMapperConfigureInfo configureInfo)
    {
        foreach (var syntaxReference in method.DeclaringSyntaxReferences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!context.SyntaxTrees.Contains(syntaxReference.SyntaxTree) ||
                syntaxReference.GetSyntax(cancellationToken) is not
                    MethodDeclarationSyntax syntax ||
                syntax.Body is null && syntax.ExpressionBody is null)
            {
                continue;
            }

            var semanticModel = context.Compilation.GetSemanticModel(
                syntax.SyntaxTree);

            if (syntax.Parent is ClassDeclarationSyntax declaration &&
                semanticModel.GetDeclaredSymbol(
                    declaration,
                    cancellationToken) is INamedTypeSymbol mapperType)
            {
                configureInfo = new TypeMapperConfigureInfo(
                    syntax,
                    mapperType,
                    Declaration: null);
                return true;
            }
        }

        configureInfo = default;
        return false;
    }

    private static INamedTypeSymbol? FindConstructedBaseType(
        INamedTypeSymbol mapperType,
        INamedTypeSymbol declaringType)
    {
        for (var current = mapperType.BaseType;
             current is not null;
             current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(
                    current.OriginalDefinition,
                    declaringType.OriginalDefinition))
            {
                return current;
            }
        }

        return null;
    }
}
