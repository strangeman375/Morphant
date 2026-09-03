using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Morphant.Generator.MapperDeclaration;
using Morphant.Generator.MappingPair;
using Morphant.Generator.PairConfiguration;
using Morphant.Generator.Settings;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class CallbackDiagnosticAnalyzer
{
    public static ImmutableArray<CallbackDiagnosticCandidate> Build(
        MapperContractAnalysis analysis,
        TypeMapperModel model,
        ImmutableArray<CallbackTransferFailureObservation> transferFailures,
        CancellationToken cancellationToken)
    {
        var candidates = ImmutableArray.CreateBuilder<
            CallbackDiagnosticCandidate>();
        var contexts = new Dictionary<string, CallbackAnalysisContext>(
            StringComparer.Ordinal);
        var staticTransferFailures = new HashSet<string>(
            StringComparer.Ordinal);

        foreach (var configuration in analysis.Configuration.Pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (analysis.Excludes(configuration.Pair.Identity) ||
                configuration.Conflicts != PairConfigurationConflict.None ||
                !TryGetMapping(
                    model,
                    configuration.Pair.Identity,
                    out var mapping) ||
                !CanAnalyze(mapping))
            {
                continue;
            }

            foreach (var callback in EnumerateCallbacks(configuration))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!IsReachable(callback, mapping))
                {
                    continue;
                }

                var callbackContext = BuildContext(
                    callback,
                    configuration,
                    mapping);
                var expressionOrigin = ExpressionOriginIdentity(
                    callback.Expression.Syntax);

                if (!contexts.ContainsKey(expressionOrigin))
                {
                    contexts.Add(expressionOrigin, callbackContext);
                }
                var callbackCandidates = AnalyzeCallback(
                    callbackContext,
                    cancellationToken);

                candidates.AddRange(callbackCandidates);

                if (callbackCandidates.Any(static candidate =>
                        candidate.IdOrder == 30))
                {
                    staticTransferFailures.Add(
                        ExpressionOriginIdentity(callback.Expression.Syntax));
                }
            }
        }

        foreach (var failure in transferFailures)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var originIdentity = ExpressionOriginIdentity(
                failure.Expression.Syntax);

            if (staticTransferFailures.Contains(originIdentity) ||
                !contexts.TryGetValue(originIdentity, out var callback))
            {
                continue;
            }

            var reason =
                $"the generated mapping reports compiler diagnostic " +
                $"'{failure.DiagnosticId}'";

            candidates.Add(CreateCandidate(
                callback,
                CallbackDiagnosticDescriptors.CallbackCannotBeTransferred,
                IdOrder: 30,
                callback.Expression.Syntax.GetLocation(),
                additionalLocations: ImmutableArray<Location>.Empty,
                detail: "compiler:" + failure.DiagnosticId,
                callback.Name,
                callback.Contract,
                callback.MapperDisplay,
                reason));
        }

        return candidates.ToImmutable();
    }

    private static ImmutableArray<CallbackDiagnosticCandidate>
        AnalyzeCallback(
            CallbackAnalysisContext context,
            CancellationToken cancellationToken)
    {
        var candidates = ImmutableArray.CreateBuilder<
            CallbackDiagnosticCandidate>();
        var core = UnwrapCallback(context.Expression);

        if (context.IsStructured && core is not LambdaExpressionSyntax)
        {
            candidates.Add(CreateCandidate(
                context,
                CallbackDiagnosticDescriptors.StructuredCallbackMustBeLambda,
                IdOrder: 29,
                core.GetLocation(),
                additionalLocations: ImmutableArray<Location>.Empty,
                detail: string.Empty,
                context.Name,
                context.Contract));
            return candidates.ToImmutable();
        }

        if (HasSourceBindingError(
                core,
                context.Expression.SemanticModel,
                cancellationToken))
        {
            return ImmutableArray<CallbackDiagnosticCandidate>.Empty;
        }

        AddUnavailableSymbolDiagnostics(
            context,
            core,
            candidates,
            cancellationToken);
        AddDeferredCaptureDiagnostics(
            context,
            core,
            candidates,
            cancellationToken);
        AddFileLocalDiagnostics(
            context,
            core,
            candidates,
            cancellationToken);
        AddExtensionBindingDiagnostics(
            context,
            core,
            candidates,
            cancellationToken);
        AddMarkerDiagnostics(
            context,
            core,
            candidates,
            cancellationToken);

        if (context.IsStructured && core is LambdaExpressionSyntax lambda)
        {
            var readOnlyMutationSpans = AddReadOnlyInputDiagnostics(
                context,
                lambda,
                candidates,
                cancellationToken);
            AddStructuredGrammarDiagnostics(
                context,
                lambda,
                readOnlyMutationSpans,
                candidates,
                cancellationToken);
        }

        return candidates.ToImmutable();
    }

    private static void AddUnavailableSymbolDiagnostics(
        CallbackAnalysisContext context,
        ExpressionSyntax core,
        ImmutableArray<CallbackDiagnosticCandidate>.Builder candidates,
        CancellationToken cancellationToken)
    {
        var references = new Dictionary<ISymbol, List<SimpleNameSyntax>>(
            SymbolEqualityComparer.Default);

        foreach (var name in core.DescendantNodesAndSelf()
                     .OfType<SimpleNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsStaticallyReachable(
                    name,
                    core,
                    context.Expression.SemanticModel,
                    cancellationToken) ||
                IsInsideNameOf(
                    name,
                    context.Expression.SemanticModel,
                    cancellationToken))
            {
                continue;
            }

            var symbol = context.Expression.SemanticModel.GetSymbolInfo(
                    name,
                    cancellationToken)
                .Symbol;

            if (!IsUnavailableConfigurationSymbol(symbol, core) ||
                !IsValueReference(
                    name,
                    symbol!,
                    context.Expression.SemanticModel,
                    cancellationToken))
            {
                continue;
            }

            if (!references.TryGetValue(symbol!, out var symbolReferences))
            {
                symbolReferences = [];
                references.Add(symbol!, symbolReferences);
            }

            symbolReferences.Add(name);
        }

        foreach (var pair in references)
        {
            var orderedReferences = pair.Value
                .OrderBy(static reference => reference.SpanStart)
                .ToImmutableArray();
            var primary = orderedReferences[0].Identifier.GetLocation();
            var additional = GetDeclarationLocations(pair.Key)
                .Concat(orderedReferences.Skip(1)
                    .Select(static reference =>
                        reference.Identifier.GetLocation()))
                .Where(location => !SameLocation(location, primary))
                .OrderBy(LocationPath, StringComparer.Ordinal)
                .ThenBy(static location => location.SourceSpan.Start)
                .ToImmutableArray();
            var symbolName = DisplaySymbol(pair.Key);
            var reason =
                $"value '{symbolName}' is only available while Configure runs";

            candidates.Add(CreateCandidate(
                context,
                CallbackDiagnosticDescriptors.CallbackCannotBeTransferred,
                IdOrder: 30,
                primary,
                additional,
                detail: "configuration:" + symbolName,
                context.Name,
                context.Contract,
                context.MapperDisplay,
                reason));
        }
    }

    private static void AddDeferredCaptureDiagnostics(
        CallbackAnalysisContext context,
        ExpressionSyntax core,
        ImmutableArray<CallbackDiagnosticCandidate>.Builder candidates,
        CancellationToken cancellationToken)
    {
        if (!context.IsStructured || core is not LambdaExpressionSyntax lambda)
        {
            return;
        }

        var roles = GetLambdaParameters(
            context,
            lambda,
            cancellationToken);

        AddDeferredParameterDiagnostic(
            context,
            core,
            roles.Previous,
            "previous",
            candidates,
            cancellationToken);
        AddDeferredParameterDiagnostic(
            context,
            core,
            roles.Result,
            "result",
            candidates,
            cancellationToken);

        if (roles.Context is null)
        {
            return;
        }

        var references = FindParameterReferences(
                core,
                roles.Context,
                context.Expression.SemanticModel,
                cancellationToken)
            .Where(reference =>
                IsStaticallyReachable(
                    reference,
                    core,
                    context.Expression.SemanticModel,
                    cancellationToken) &&
                IsMappingOperationAccess(
                    reference,
                    context.Expression.SemanticModel,
                    cancellationToken) &&
                IsDeferred(reference, core))
            .ToImmutableArray();

        if (references.IsEmpty)
        {
            return;
        }

        var primary = references[0].Identifier.GetLocation();
        var additional = GetDeclarationLocations(roles.Context)
            .Concat(references.Skip(1)
                .Select(static reference =>
                    reference.Identifier.GetLocation()))
            .Where(location => !SameLocation(location, primary))
            .OrderBy(LocationPath, StringComparer.Ordinal)
            .ThenBy(static location => location.SourceSpan.Start)
            .ToImmutableArray();

        candidates.Add(CreateCandidate(
            context,
            CallbackDiagnosticDescriptors.CallbackCannotBeTransferred,
            IdOrder: 30,
            primary,
            additional,
            detail: "deferred:context-operation",
            context.Name,
            context.Contract,
            context.MapperDisplay,
            "'context.Operation' cannot be used inside a nested lambda or " +
            "local function"));
    }

    private static void AddDeferredParameterDiagnostic(
        CallbackAnalysisContext context,
        ExpressionSyntax core,
        IParameterSymbol? parameter,
        string parameterName,
        ImmutableArray<CallbackDiagnosticCandidate>.Builder candidates,
        CancellationToken cancellationToken)
    {
        if (parameter is null)
        {
            return;
        }

        var references = FindParameterReferences(
                core,
                parameter,
                context.Expression.SemanticModel,
                cancellationToken)
            .Where(reference =>
                IsStaticallyReachable(
                    reference,
                    core,
                    context.Expression.SemanticModel,
                    cancellationToken) &&
                IsDeferred(reference, core))
            .ToImmutableArray();

        if (references.IsEmpty)
        {
            return;
        }

        var primary = references[0].Identifier.GetLocation();
        var additional = GetDeclarationLocations(parameter)
            .Concat(references.Skip(1)
                .Select(static reference =>
                    reference.Identifier.GetLocation()))
            .Where(location => !SameLocation(location, primary))
            .OrderBy(LocationPath, StringComparer.Ordinal)
            .ThenBy(static location => location.SourceSpan.Start)
            .ToImmutableArray();
        var reason =
            $"'{parameterName}' cannot be used inside a nested lambda or " +
            "local function";

        candidates.Add(CreateCandidate(
            context,
            CallbackDiagnosticDescriptors.CallbackCannotBeTransferred,
            IdOrder: 30,
            primary,
            additional,
            detail: "deferred:" + parameterName,
            context.Name,
            context.Contract,
            context.MapperDisplay,
            reason));
    }

    private static void AddFileLocalDiagnostics(
        CallbackAnalysisContext context,
        ExpressionSyntax core,
        ImmutableArray<CallbackDiagnosticCandidate>.Builder candidates,
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        foreach (var name in core.DescendantNodesAndSelf()
                     .OfType<SimpleNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsStaticallyReachable(
                    name,
                    core,
                    context.Expression.SemanticModel,
                    cancellationToken) ||
                IsInsideNameOf(
                    name,
                    context.Expression.SemanticModel,
                    cancellationToken))
            {
                continue;
            }

            var symbol = context.Expression.SemanticModel.GetSymbolInfo(
                    name,
                    cancellationToken)
                .Symbol;

            if (symbol is INamedTypeSymbol &&
                name.Parent is MemberAccessExpressionSyntax memberAccess &&
                ReferenceEquals(memberAccess.Expression, name))
            {
                continue;
            }

            if (symbol is null ||
                !IsFileLocal(symbol) ||
                !seen.Add(symbol.OriginalDefinition))
            {
                continue;
            }

            var display = DisplaySymbol(symbol);
            var reason = $"file-local symbol '{display}' is inaccessible";

            candidates.Add(CreateCandidate(
                context,
                CallbackDiagnosticDescriptors.CallbackCannotBeTransferred,
                IdOrder: 30,
                name.Identifier.GetLocation(),
                additionalLocations: ImmutableArray<Location>.Empty,
                detail: "file-local:" + display,
                context.Name,
                context.Contract,
                context.MapperDisplay,
                reason));
        }
    }

    private static void AddExtensionBindingDiagnostics(
        CallbackAnalysisContext context,
        ExpressionSyntax core,
        ImmutableArray<CallbackDiagnosticCandidate>.Builder candidates,
        CancellationToken cancellationToken)
    {
        foreach (var name in core.DescendantNodesAndSelf()
                     .OfType<SimpleNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsStaticallyReachable(
                    name,
                    core,
                    context.Expression.SemanticModel,
                    cancellationToken) ||
                IsInsideNameOf(
                    name,
                    context.Expression.SemanticModel,
                    cancellationToken))
            {
                continue;
            }

            var symbol = context.Expression.SemanticModel.GetSymbolInfo(
                    name,
                    cancellationToken)
                .Symbol as IMethodSymbol;

            if (symbol is null ||
                !(symbol.ReducedFrom ?? symbol).IsExtensionMethod ||
                IsInvocationTarget(name))
            {
                continue;
            }

            candidates.Add(CreateTransferBindingCandidate(
                context,
                name.Identifier.GetLocation(),
                "extension method group"));
        }

        foreach (var query in core.DescendantNodesAndSelf()
                     .OfType<QueryExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsStaticallyReachable(
                    query,
                    core,
                    context.Expression.SemanticModel,
                    cancellationToken) ||
                DeclarativeQueryExpressionPolicy.IsSupported(
                    query,
                    context.Expression.SemanticModel,
                    cancellationToken))
            {
                continue;
            }

            candidates.Add(CreateTransferBindingCandidate(
                context,
                query.FromClause.FromKeyword.GetLocation(),
                "custom query pattern"));
        }

        if (context.IsStructured)
        {
            return;
        }

        foreach (var statement in core.DescendantNodesAndSelf()
                     .OfType<ForEachStatementSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsStaticallyReachable(
                    statement,
                    core,
                    context.Expression.SemanticModel,
                    cancellationToken))
            {
                continue;
            }

            var info = context.Expression.SemanticModel
                .GetForEachStatementInfo(statement);
            var getEnumerator = info.GetEnumeratorMethod;

            if (getEnumerator is null ||
                !(getEnumerator.ReducedFrom ?? getEnumerator)
                    .IsExtensionMethod)
            {
                continue;
            }

            candidates.Add(CreateTransferBindingCandidate(
                context,
                statement.ForEachKeyword.GetLocation(),
                "extension foreach pattern"));
        }
    }

    private static CallbackDiagnosticCandidate CreateTransferBindingCandidate(
        CallbackAnalysisContext context,
        Location location,
        string construct)
    {
        var reason = $"{construct} is not supported";

        return CreateCandidate(
            context,
            CallbackDiagnosticDescriptors.CallbackCannotBeTransferred,
            IdOrder: 30,
            location,
            additionalLocations: ImmutableArray<Location>.Empty,
            detail: "extension:" + construct,
            context.Name,
            context.Contract,
            context.MapperDisplay,
            reason);
    }

    private static void AddMarkerDiagnostics(
        CallbackAnalysisContext context,
        ExpressionSyntax core,
        ImmutableArray<CallbackDiagnosticCandidate>.Builder candidates,
        CancellationToken cancellationToken)
    {
        foreach (var name in core.DescendantNodesAndSelf()
                     .OfType<SimpleNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsStaticallyReachable(
                    name,
                    core,
                    context.Expression.SemanticModel,
                    cancellationToken) ||
                IsInsideNameOf(
                    name,
                    context.Expression.SemanticModel,
                    cancellationToken) ||
                !DeclarativeIntrinsic.TryGetKind(
                    name,
                    context.Expression.SemanticModel,
                    cancellationToken,
                    out var kind))
            {
                continue;
            }

            if (context.IsStructured &&
                IsSupportedStructuredMarkerUse(
                    name,
                    core,
                    context.Expression.SemanticModel,
                    cancellationToken))
            {
                continue;
            }

            candidates.Add(CreateCandidate(
                context,
                CallbackDiagnosticDescriptors.InvalidCompileTimeMarkerUse,
                IdOrder: 33,
                name.Identifier.GetLocation(),
                additionalLocations: ImmutableArray<Location>.Empty,
                detail: "marker:" + kind,
                kind.ToString(),
                context.Name,
                context.Contract));
        }

        if (!context.IsStructured || core is not LambdaExpressionSyntax lambda)
        {
            return;
        }

        var contextParameter = GetLambdaParameters(
                context,
                lambda,
                cancellationToken)
            .Context;

        if (contextParameter is null)
        {
            return;
        }

        foreach (var reference in FindParameterReferences(
                     core,
                     contextParameter,
                     context.Expression.SemanticModel,
                     cancellationToken))
        {
            if (!IsStaticallyReachable(
                    reference,
                    core,
                    context.Expression.SemanticModel,
                    cancellationToken) ||
                IsMappingOperationAccess(
                    reference,
                    context.Expression.SemanticModel,
                    cancellationToken))
            {
                continue;
            }

            candidates.Add(CreateCandidate(
                context,
                CallbackDiagnosticDescriptors.InvalidCompileTimeMarkerUse,
                IdOrder: 33,
                reference.Identifier.GetLocation(),
                additionalLocations: ImmutableArray<Location>.Empty,
                detail: "marker:MappingContextMarker:" +
                    reference.SpanStart,
                "context",
                context.Name,
                context.Contract));
        }
    }

    private static ImmutableHashSet<TextSpanKey> AddReadOnlyInputDiagnostics(
        CallbackAnalysisContext context,
        LambdaExpressionSyntax lambda,
        ImmutableArray<CallbackDiagnosticCandidate>.Builder candidates,
        CancellationToken cancellationToken)
    {
        var roles = GetLambdaParameters(context, lambda, cancellationToken);
        var aliases = BuildDestinationAliases(
            lambda,
            roles.Previous,
            roles.Result,
            context.Expression.SemanticModel,
            cancellationToken);
        var mutationSpans = ImmutableHashSet.CreateBuilder<TextSpanKey>();

        foreach (var mutation in EnumerateMutations(lambda))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsStaticallyReachable(
                    mutation.Node,
                    lambda,
                    context.Expression.SemanticModel,
                    cancellationToken) ||
                !TryGetDestinationRoot(
                    mutation.Target,
                    aliases,
                    roles.Previous,
                    roles.Result,
                    context.Expression.SemanticModel,
                    cancellationToken,
                    out var inputName))
            {
                continue;
            }

            mutationSpans.Add(TextSpanKey.Create(mutation.Node));
            candidates.Add(CreateCandidate(
                context,
                CallbackDiagnosticDescriptors.StructuredInputIsReadOnly,
                IdOrder: 32,
                mutation.Location,
                additionalLocations: ImmutableArray<Location>.Empty,
                detail: inputName + ":" + mutation.Location.SourceSpan.Start,
                inputName,
                context.Contract));
        }

        return mutationSpans.ToImmutable();
    }

    private static void AddStructuredGrammarDiagnostics(
        CallbackAnalysisContext context,
        LambdaExpressionSyntax lambda,
        ISet<TextSpanKey> readOnlyMutationSpans,
        ImmutableArray<CallbackDiagnosticCandidate>.Builder candidates,
        CancellationToken cancellationToken)
    {
        if (lambda.Block is null)
        {
            return;
        }

        foreach (var unsupported in EnumerateUnsupportedStatements(
                     lambda.Block,
                     lambda,
                     readOnlyMutationSpans,
                     context.Expression.SemanticModel,
                     cancellationToken))
        {
            candidates.Add(CreateCandidate(
                context,
                CallbackDiagnosticDescriptors.UnsupportedStructuredSyntax,
                IdOrder: 31,
                unsupported.Location,
                additionalLocations: ImmutableArray<Location>.Empty,
                detail: unsupported.Name + ":" +
                    unsupported.Location.SourceSpan.Start,
                context.Name,
                context.Contract,
                unsupported.Name));
        }

        foreach (var mutation in EnumerateUnsupportedStructuredMutations(
                     lambda,
                     readOnlyMutationSpans,
                     context.Expression.SemanticModel,
                     cancellationToken))
        {
            candidates.Add(CreateCandidate(
                context,
                CallbackDiagnosticDescriptors.UnsupportedStructuredSyntax,
                IdOrder: 31,
                mutation.Location,
                additionalLocations: ImmutableArray<Location>.Empty,
                detail: mutation.Name + ":" +
                    mutation.Location.SourceSpan.Start,
                context.Name,
                context.Contract,
                mutation.Name));
        }
    }

    private static bool CanAnalyze(TypeMapperMappingModel mapping)
    {
        if (mapping.Failure is { } failure &&
            IsPriorCategoryFailure(failure.Reason))
        {
            return false;
        }

        var settings = mapping.EffectiveSettings;

        return settings.HasExecutableOperation &&
               (settings.SupportsCreate &&
                    mapping.CreateOperationFailure is null ||
                settings.SupportsUpdate &&
                    mapping.UpdateOperationFailure is null);
    }

    private static bool IsPriorCategoryFailure(MappingFailureReason reason)
    {
        return reason is
            MappingFailureReason.UnsupportedMappingContract or
            MappingFailureReason.InvalidBaseConfiguration or
            MappingFailureReason.UnsupportedMapperBuilderFlow or
            MappingFailureReason.UnsupportedMappingBuilderFlow or
            MappingFailureReason.InvalidPairConfiguration or
            MappingFailureReason.InvalidManualSetting or
            MappingFailureReason.InvalidSetting or
            MappingFailureReason.InapplicableSetting;
    }

    private static bool IsReachable(
        CallbackModel callback,
        TypeMapperMappingModel mapping)
    {
        var settings = mapping.EffectiveSettings;
        var create = settings.SupportsCreate &&
                     mapping.CreateOperationFailure is null;
        var update = settings.SupportsUpdate &&
                     mapping.UpdateOperationFailure is null;

        if (callback.Name is "Construct" or "ConstructUsing")
        {
            return create ||
                   update &&
                   mapping.DestinationCanBeNull &&
                   settings.NullDestinationHandling ==
                       NullDestinationHandlingValue.Create;
        }

        return create || update;
    }

    private static bool TryGetMapping(
        TypeMapperModel model,
        MappingPairIdentity identity,
        out TypeMapperMappingModel mapping)
    {
        foreach (var candidate in model.Mappings)
        {
            if (StringComparer.Ordinal.Equals(
                    candidate.AnalysisContext.Identity.Source.Key,
                    identity.Source.Key) &&
                StringComparer.Ordinal.Equals(
                    candidate.AnalysisContext.Identity.Destination.Key,
                    identity.Destination.Key))
            {
                mapping = candidate;
                return true;
            }
        }

        mapping = default;
        return false;
    }

    private static IEnumerable<CallbackModel> EnumerateCallbacks(
        PairConfigurationModel configuration)
    {
        foreach (var policy in configuration.Declarative.ResultPolicies)
        {
            yield return new CallbackModel(
                policy.Kind.ToString(),
                policy.Kind is
                    ResultPolicyKind.Construct or ResultPolicyKind.Resolve,
                policy.Invocation,
                policy.Expression,
                policy.Form.ToString());
        }

        foreach (var members in configuration.Declarative.Members)
        {
            yield return new CallbackModel(
                "Members",
                IsStructured: true,
                members.Invocation,
                members.Expression,
                members.Form.ToString());
        }

        foreach (var conversion in configuration.Manual.Conversions)
        {
            yield return new CallbackModel(
                "Convert",
                IsStructured: false,
                conversion.Invocation,
                conversion.Expression,
                conversion.Form.ToString());
        }
    }

    private static CallbackAnalysisContext BuildContext(
        CallbackModel callback,
        PairConfigurationModel configuration,
        TypeMapperMappingModel mapping)
    {
        var (sourceType, destinationType) = GetCallbackContract(
            callback,
            configuration);
        var mapper = callback.Expression.DeclaringMapperType
            .OriginalDefinition;
        var callbackOriginIdentity = CallbackOriginIdentity(
            callback.Expression.Syntax,
            callback.Name);

        return new CallbackAnalysisContext(
            callback.Name,
            callback.IsStructured,
            callback.Form,
            callback.Invocation,
            callback.Expression,
            MapperContractDisplay.Create(sourceType, destinationType),
            MappingTypeIdentityPolicy.Create(sourceType).Key + "->" +
                MappingTypeIdentityPolicy.Create(destinationType).Key,
            SymbolNameHelper.GetFullMetadataName(mapper),
            MapperContractDisplay.CreateType(mapper),
            callback.Expression.DeclaringLevelOrder,
            callbackOriginIdentity,
            SymbolEqualityComparer.Default.Equals(
                configuration.Origin.DeclaringMapperType.OriginalDefinition,
                mapper),
            mapping.AnalysisContext);
    }

    private static (ITypeSymbol Source, ITypeSymbol Destination)
        GetCallbackContract(
            CallbackModel callback,
            PairConfigurationModel fallback)
    {
        if (callback.Invocation.Expression is
                MemberAccessExpressionSyntax memberAccess &&
            callback.Expression.SemanticModel.GetTypeInfo(
                    memberAccess.Expression)
                .Type is INamedTypeSymbol
                {
                    TypeArguments.Length: 3
                } builderType &&
            StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    builderType.OriginalDefinition),
                MetadataNames.PairMapperBuilder))
        {
            return (builderType.TypeArguments[1], builderType.TypeArguments[2]);
        }

        return (fallback.Pair.SourceType, fallback.Pair.DestinationType);
    }

    private static CallbackDiagnosticCandidate CreateCandidate(
        CallbackAnalysisContext context,
        DiagnosticDescriptor descriptor,
        int IdOrder,
        Location location,
        ImmutableArray<Location> additionalLocations,
        string detail,
        params object[] messageArguments)
    {
        var identity = descriptor.Id + "|" +
                       LocationIdentity(location) + "|" + detail;

        return new CallbackDiagnosticCandidate(
            IdOrder,
            identity,
            context.CallbackOriginIdentity,
            context.MapperIdentity,
            context.LevelOrder,
            context.PairKey,
            location.SourceSpan.Start,
            detail,
            context.IsDeclaringOrigin,
            Diagnostic.Create(
                descriptor,
                location,
                additionalLocations,
                properties: null,
                messageArguments));
    }

    private static string CallbackOriginIdentity(
        ExpressionSyntax syntax,
        string callbackName)
    {
        return callbackName + "|" + LocationIdentity(syntax.GetLocation());
    }

    private static string ExpressionOriginIdentity(ExpressionSyntax syntax)
    {
        return LocationIdentity(syntax.GetLocation());
    }

    private static string LocationIdentity(Location location)
    {
        return LocationPath(location) + "|" +
               location.SourceSpan.Start + "|" +
               location.SourceSpan.Length;
    }

    private static string LocationPath(Location location) =>
        location.SourceTree?.FilePath ?? string.Empty;

    private static bool SameLocation(Location left, Location right)
    {
        return ReferenceEquals(left.SourceTree, right.SourceTree) &&
               left.SourceSpan == right.SourceSpan;
    }

    private static ExpressionSyntax UnwrapCallback(
        BoundConfigurationExpression expression)
    {
        var current = expression.Syntax;

        while (true)
        {
            switch (current)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    current = parenthesized.Expression;
                    continue;

                case PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(
                        SyntaxKind.SuppressNullableWarningExpression):
                    current = postfix.Operand;
                    continue;

                case CastExpressionSyntax cast
                    when expression.SemanticModel.GetTypeInfo(cast.Type).Type is
                            INamedTypeSymbol castType &&
                         SymbolEqualityComparer.IncludeNullability.Equals(
                             castType,
                             expression.DelegateType):
                    current = cast.Expression;
                    continue;

                default:
                    return current;
            }
        }
    }

    private static bool HasSourceBindingError(
        SyntaxNode syntax,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return semanticModel.GetDiagnostics(syntax.Span, cancellationToken)
            .Any(static diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error);
    }

    private static bool IsStaticallyReachable(
        SyntaxNode node,
        SyntaxNode root,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        for (var current = node;
             current.Parent is { } parent &&
             !ReferenceEquals(current, root);
             current = parent)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (parent)
            {
                case IfStatementSyntax ifStatement:
                    var ifCondition = GetBooleanConstant(
                        ifStatement.Condition,
                        semanticModel,
                        cancellationToken);

                    if ((ifCondition == false &&
                         ReferenceEquals(current, ifStatement.Statement)) ||
                        (ifCondition == true &&
                         ReferenceEquals(current, ifStatement.Else)))
                    {
                        return false;
                    }

                    break;

                case ConditionalExpressionSyntax conditional:
                    var conditionalCondition = GetBooleanConstant(
                        conditional.Condition,
                        semanticModel,
                        cancellationToken);

                    if ((conditionalCondition == false &&
                         ReferenceEquals(current, conditional.WhenTrue)) ||
                        (conditionalCondition == true &&
                         ReferenceEquals(current, conditional.WhenFalse)))
                    {
                        return false;
                    }

                    break;

                case BinaryExpressionSyntax binary
                    when ReferenceEquals(current, binary.Right):
                    var left = GetBooleanConstant(
                        binary.Left,
                        semanticModel,
                        cancellationToken);

                    if ((binary.IsKind(
                             SyntaxKind.LogicalAndExpression) &&
                         left == false) ||
                        (binary.IsKind(
                             SyntaxKind.LogicalOrExpression) &&
                         left == true))
                    {
                        return false;
                    }

                    break;
            }
        }

        return true;
    }

    private static bool? GetBooleanConstant(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var value = semanticModel.GetConstantValue(
            expression,
            cancellationToken);

        return value.HasValue && value.Value is bool boolean
            ? boolean
            : null;
    }

    private static bool IsUnavailableConfigurationSymbol(
        ISymbol? symbol,
        SyntaxNode callback)
    {
        if (symbol is ILocalSymbol { IsConst: true })
        {
            return false;
        }

        if (symbol is not (ILocalSymbol or IParameterSymbol) &&
            symbol is not IMethodSymbol
            {
                MethodKind: MethodKind.LocalFunction
            })
        {
            return false;
        }

        return !IsDeclaredWithin(symbol, callback);
    }

    private static bool IsValueReference(
        SimpleNameSyntax syntax,
        ISymbol symbol,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (syntax.Parent is NameColonSyntax or NameEqualsSyntax)
        {
            return false;
        }

        if (IsInsideNameOf(
                syntax,
                semanticModel,
                cancellationToken))
        {
            return false;
        }

        if (symbol is IMethodSymbol
            {
                MethodKind: MethodKind.LocalFunction
            })
        {
            return true;
        }

        return semanticModel.GetOperation(syntax, cancellationToken) is
            ILocalReferenceOperation or IParameterReferenceOperation;
    }

    private static bool IsInsideNameOf(
        SyntaxNode syntax,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return syntax.Ancestors()
            .OfType<InvocationExpressionSyntax>()
            .Any(invocation =>
                semanticModel.GetOperation(
                    invocation,
                    cancellationToken) is INameOfOperation);
    }

    private static bool IsDeclaredWithin(ISymbol symbol, SyntaxNode syntax)
    {
        return symbol.DeclaringSyntaxReferences.Any(reference =>
            ReferenceEquals(reference.SyntaxTree, syntax.SyntaxTree) &&
            syntax.FullSpan.Contains(reference.Span));
    }

    private static ImmutableArray<Location> GetDeclarationLocations(
        ISymbol symbol)
    {
        return symbol.DeclaringSyntaxReferences
            .Select(static reference => reference.GetSyntax())
            .Select(GetDeclarationLocation)
            .ToImmutableArray();
    }

    private static Location GetDeclarationLocation(SyntaxNode syntax)
    {
        return syntax switch
        {
            VariableDeclaratorSyntax variable =>
                variable.Identifier.GetLocation(),
            ParameterSyntax parameter => parameter.Identifier.GetLocation(),
            LocalFunctionStatementSyntax function =>
                function.Identifier.GetLocation(),
            SingleVariableDesignationSyntax designation =>
                designation.Identifier.GetLocation(),
            _ => syntax.GetLocation()
        };
    }

    private static string DisplaySymbol(ISymbol symbol)
    {
        return symbol switch
        {
            ILocalSymbol or IParameterSymbol => symbol.Name,
            _ => symbol.ToDisplayString(
                SymbolDisplayFormat.CSharpErrorMessageFormat)
        };
    }

    private static ImmutableArray<IdentifierNameSyntax>
        FindParameterReferences(
            SyntaxNode syntax,
            IParameterSymbol parameter,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
    {
        return syntax.DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(identifier =>
                SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(
                            identifier,
                            cancellationToken)
                        .Symbol,
                    parameter))
            .OrderBy(static identifier => identifier.SpanStart)
            .ToImmutableArray();
    }

    private static bool IsDeferred(SyntaxNode node, SyntaxNode root)
    {
        for (var current = node.Parent;
             current is not null && !ReferenceEquals(current, root);
             current = current.Parent)
        {
            if (current is AnonymousFunctionExpressionSyntax or
                LocalFunctionStatementSyntax)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMappingOperationAccess(
        IdentifierNameSyntax reference,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        SyntaxNode current = reference;

        while (current.Parent is ParenthesizedExpressionSyntax parenthesized &&
               ReferenceEquals(parenthesized.Expression, current) ||
               current.Parent is PostfixUnaryExpressionSyntax postfix &&
               postfix.IsKind(
                   SyntaxKind.SuppressNullableWarningExpression) &&
               ReferenceEquals(postfix.Operand, current))
        {
            current = current.Parent!;
        }

        if (current.Parent is not MemberAccessExpressionSyntax access ||
            !ReferenceEquals(access.Expression, current) ||
            access.Name.Identifier.ValueText != "Operation")
        {
            return false;
        }

        return semanticModel.GetSymbolInfo(access, cancellationToken).Symbol is
            IPropertySymbol property &&
            SymbolNameHelper.GetFullMetadataName(
                property.ContainingType) ==
            MetadataNames.MappingContextMarker;
    }

    private static bool IsFileLocal(ISymbol symbol)
    {
        if (symbol is INamedTypeSymbol { IsFileLocal: true })
        {
            return true;
        }

        for (var type = symbol.ContainingType;
             type is not null;
             type = type.ContainingType)
        {
            if (type.IsFileLocal)
            {
                return true;
            }
        }

        return symbol switch
        {
            IMethodSymbol method => ContainsFileLocalType(method.ReturnType) ||
                                    method.Parameters.Any(parameter =>
                                        ContainsFileLocalType(parameter.Type)),
            IPropertySymbol property =>
                ContainsFileLocalType(property.Type),
            IFieldSymbol field => ContainsFileLocalType(field.Type),
            ILocalSymbol local => ContainsFileLocalType(local.Type),
            _ => false
        };
    }

    private static bool ContainsFileLocalType(ITypeSymbol type)
    {
        return type switch
        {
            INamedTypeSymbol named => named.IsFileLocal ||
                named.TypeArguments.Any(ContainsFileLocalType),
            IArrayTypeSymbol array => ContainsFileLocalType(array.ElementType),
            IPointerTypeSymbol pointer =>
                ContainsFileLocalType(pointer.PointedAtType),
            _ => false
        };
    }

    private static bool IsInvocationTarget(SimpleNameSyntax name)
    {
        if (name.Parent is InvocationExpressionSyntax direct)
        {
            return ReferenceEquals(direct.Expression, name);
        }

        if (name.Parent is MemberAccessExpressionSyntax access &&
            ReferenceEquals(access.Name, name) &&
            access.Parent is InvocationExpressionSyntax memberInvocation)
        {
            return ReferenceEquals(memberInvocation.Expression, access);
        }

        if (name.Parent is MemberBindingExpressionSyntax binding &&
            ReferenceEquals(binding.Name, name) &&
            binding.Parent is InvocationExpressionSyntax boundInvocation)
        {
            return ReferenceEquals(boundInvocation.Expression, binding);
        }

        return false;
    }

    private static bool IsSupportedStructuredMarkerUse(
        SimpleNameSyntax name,
        ExpressionSyntax callbackRoot,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var markerExpression = GetMarkerExpression(name);

        if (markerExpression is null)
        {
            return false;
        }

        if (markerExpression.Parent is ExpressionStatementSyntax &&
            DeclarativeNestedMapExpression.IsNestedUpdateStatement(
                markerExpression,
                semanticModel,
                cancellationToken))
        {
            return true;
        }

        if (TryGetTerminalRoot(
                markerExpression,
                callbackRoot,
                out var terminalRoot) &&
            DeclarativeIntrinsic.HasSupportedTerminalPlacement(
                terminalRoot,
                markerExpression,
                semanticModel,
                cancellationToken))
        {
            return true;
        }

        var initializer = markerExpression.Ancestors()
            .OfType<EqualsValueClauseSyntax>()
            .FirstOrDefault(candidate =>
                candidate.Parent is VariableDeclaratorSyntax &&
                callbackRoot.FullSpan.Contains(candidate.FullSpan));

        if (initializer is not null &&
            initializer.Parent is VariableDeclaratorSyntax variable &&
            DeclarativeIntrinsic.HasSupportedTerminalPlacement(
                initializer.Value,
                markerExpression,
                semanticModel,
                cancellationToken) &&
            semanticModel.GetDeclaredSymbol(
                    variable,
                    cancellationToken) is ILocalSymbol local)
        {
            var references = callbackRoot.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Where(reference =>
                    IsStaticallyReachable(
                        reference,
                        callbackRoot,
                        semanticModel,
                        cancellationToken) &&
                    SymbolEqualityComparer.Default.Equals(
                        semanticModel.GetSymbolInfo(
                                reference,
                                cancellationToken)
                            .Symbol,
                        local))
                .ToImmutableArray();

            return !references.IsEmpty &&
                   AreLocalReferencesTerminal(
                       local,
                       callbackRoot,
                       semanticModel,
                       cancellationToken,
                       new HashSet<ILocalSymbol>(
                           SymbolEqualityComparer.Default));
        }

        return false;
    }

    private static bool AreLocalReferencesTerminal(
        ILocalSymbol local,
        ExpressionSyntax callbackRoot,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ISet<ILocalSymbol> visiting)
    {
        if (!visiting.Add(local))
        {
            return false;
        }

        var references = callbackRoot.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(reference =>
                IsStaticallyReachable(
                    reference,
                    callbackRoot,
                    semanticModel,
                    cancellationToken) &&
                SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(
                            reference,
                            cancellationToken)
                        .Symbol,
                    local))
            .ToImmutableArray();

        var result = !references.IsEmpty && references.All(reference =>
            IsEventuallyTerminal(
                reference,
                callbackRoot,
                semanticModel,
                cancellationToken,
                visiting));

        visiting.Remove(local);
        return result;
    }

    private static bool IsEventuallyTerminal(
        IdentifierNameSyntax reference,
        ExpressionSyntax callbackRoot,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        ISet<ILocalSymbol> visiting)
    {
        if (TryGetTerminalRoot(
                reference,
                callbackRoot,
                out var terminalRoot) &&
            DeclarativeIntrinsic.HasSupportedTerminalPlacement(
                terminalRoot,
                reference,
                semanticModel,
                cancellationToken))
        {
            return true;
        }

        var initializer = reference.Ancestors()
            .OfType<EqualsValueClauseSyntax>()
            .FirstOrDefault(candidate =>
                candidate.Parent is VariableDeclaratorSyntax &&
                callbackRoot.FullSpan.Contains(candidate.FullSpan));

        return initializer is not null &&
               initializer.Parent is VariableDeclaratorSyntax variable &&
               DeclarativeIntrinsic.HasSupportedTerminalPlacement(
                   initializer.Value,
                   reference,
                   semanticModel,
                   cancellationToken) &&
               semanticModel.GetDeclaredSymbol(
                   variable,
                   cancellationToken) is ILocalSymbol downstream &&
               AreLocalReferencesTerminal(
                   downstream,
                   callbackRoot,
                   semanticModel,
                   cancellationToken,
                   visiting);
    }

    private static InvocationExpressionSyntax? GetMarkerExpression(
        SimpleNameSyntax name)
    {
        return FindContainingInvocation(name);
    }

    private static InvocationExpressionSyntax? FindContainingInvocation(
        SimpleNameSyntax name)
    {
        if (name.Parent is InvocationExpressionSyntax direct &&
            ReferenceEquals(direct.Expression, name))
        {
            return direct;
        }

        if (name.Parent is MemberAccessExpressionSyntax access &&
            ReferenceEquals(access.Name, name) &&
            access.Parent is InvocationExpressionSyntax invocation &&
            ReferenceEquals(invocation.Expression, access))
        {
            return invocation;
        }

        return null;
    }

    private static bool TryGetTerminalRoot(
        ExpressionSyntax expression,
        ExpressionSyntax callbackRoot,
        out ExpressionSyntax root)
    {
        for (SyntaxNode current = expression;
             current.Parent is { } parent &&
             callbackRoot.FullSpan.Contains(parent.FullSpan);
             current = parent)
        {
            switch (parent)
            {
                case ArgumentSyntax argument
                    when argument.Parent?.Parent is
                        ObjectCreationExpressionSyntax or
                        ImplicitObjectCreationExpressionSyntax:
                    root = argument.Expression;
                    return true;

                case AssignmentExpressionSyntax assignment
                    when ReferenceEquals(assignment.Right, current) &&
                         assignment.Parent is InitializerExpressionSyntax:
                    root = assignment.Right;
                    return true;

                case EqualsValueClauseSyntax:
                case AnonymousFunctionExpressionSyntax:
                case LocalFunctionStatementSyntax:
                    root = expression;
                    return false;
            }
        }

        root = expression;
        return false;
    }

    private static LambdaParameterRoles GetLambdaParameters(
        CallbackAnalysisContext context,
        LambdaExpressionSyntax lambda,
        CancellationToken cancellationToken)
    {
        var syntaxParameters = lambda switch
        {
            SimpleLambdaExpressionSyntax simple =>
                ImmutableArray.Create(simple.Parameter),
            ParenthesizedLambdaExpressionSyntax parenthesized =>
                parenthesized.ParameterList.Parameters.ToImmutableArray(),
            _ => ImmutableArray<ParameterSyntax>.Empty
        };
        var parameters = syntaxParameters
            .Select(parameter =>
                context.Expression.SemanticModel.GetDeclaredSymbol(
                    parameter,
                    cancellationToken) as IParameterSymbol)
            .ToImmutableArray();
        IParameterSymbol? At(int index) =>
            index >= 0 && index < parameters.Length
                ? parameters[index]
                : null;
        var hasPrevious = context.Name is
                "Resolve" or "ResolveUsing" ||
            context.Name == "Members" && parameters.Length >= 2 ||
            context.Name == "Convert" && parameters.Length >= 2;
        var hasResult = context.Name == "Members" && parameters.Length >= 3;
        var hasContext = context.Form.Contains(
            "Context",
            StringComparison.Ordinal);

        return new LambdaParameterRoles(
            At(0),
            hasPrevious ? At(1) : null,
            hasResult ? At(2) : null,
            hasContext ? At(parameters.Length - 1) : null);
    }

    private static Dictionary<ISymbol, string> BuildDestinationAliases(
        LambdaExpressionSyntax lambda,
        IParameterSymbol? previous,
        IParameterSymbol? result,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var aliases = new Dictionary<ISymbol, string>(
            SymbolEqualityComparer.Default);

        if (previous is not null)
        {
            aliases[previous] = "previous";
        }

        if (result is not null)
        {
            aliases[result] = "result";
        }

        var changed = true;

        while (changed)
        {
            changed = false;

            foreach (var variable in lambda.DescendantNodes()
                         .OfType<VariableDeclaratorSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (variable.Initializer?.Value is not { } initializer ||
                    semanticModel.GetDeclaredSymbol(
                        variable,
                        cancellationToken) is not ILocalSymbol local ||
                    aliases.ContainsKey(local) ||
                    !TryGetDestinationRoot(
                        initializer,
                        aliases,
                        previous,
                        result,
                        semanticModel,
                        cancellationToken,
                        out var inputName))
                {
                    continue;
                }

                aliases.Add(local, inputName);
                changed = true;
            }

            foreach (var pattern in lambda.DescendantNodes()
                         .OfType<DeclarationPatternSyntax>())
            {
                if (pattern.Designation is not
                        SingleVariableDesignationSyntax designation ||
                    semanticModel.GetDeclaredSymbol(
                        designation,
                        cancellationToken) is not ILocalSymbol local ||
                    aliases.ContainsKey(local) ||
                    pattern.Parent is not IsPatternExpressionSyntax isPattern ||
                    !TryGetDestinationRoot(
                        isPattern.Expression,
                        aliases,
                        previous,
                        result,
                        semanticModel,
                        cancellationToken,
                        out var inputName))
                {
                    continue;
                }

                aliases.Add(local, inputName);
                changed = true;
            }
        }

        return aliases;
    }

    private static bool TryGetDestinationRoot(
        ExpressionSyntax expression,
        IReadOnlyDictionary<ISymbol, string> aliases,
        IParameterSymbol? previous,
        IParameterSymbol? result,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out string inputName)
    {
        expression = UnwrapTransparent(expression);
        var symbol = semanticModel.GetSymbolInfo(
                expression,
                cancellationToken)
            .Symbol;

        if (symbol is not null && aliases.TryGetValue(symbol, out inputName!))
        {
            return true;
        }

        switch (expression)
        {
            case MemberAccessExpressionSyntax access:
                return TryGetDestinationRoot(
                    access.Expression,
                    aliases,
                    previous,
                    result,
                    semanticModel,
                    cancellationToken,
                    out inputName);

            case ElementAccessExpressionSyntax element:
                return TryGetDestinationRoot(
                    element.Expression,
                    aliases,
                    previous,
                    result,
                    semanticModel,
                    cancellationToken,
                    out inputName);

            case CastExpressionSyntax cast:
                var conversion = semanticModel.Compilation.ClassifyConversion(
                    semanticModel.GetTypeInfo(
                            cast.Expression,
                            cancellationToken)
                        .Type!,
                    semanticModel.GetTypeInfo(cast.Type, cancellationToken)
                        .Type!);

                if (conversion.IsIdentity ||
                    conversion.IsImplicit && conversion.IsReference)
                {
                    return TryGetDestinationRoot(
                        cast.Expression,
                        aliases,
                        previous,
                        result,
                        semanticModel,
                        cancellationToken,
                        out inputName);
                }

                break;
        }

        inputName = string.Empty;
        return false;
    }

    private static ExpressionSyntax UnwrapTransparent(
        ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    continue;
                case PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(
                        SyntaxKind.SuppressNullableWarningExpression):
                    expression = postfix.Operand;
                    continue;
                default:
                    return expression;
            }
        }
    }

    private static IEnumerable<MutationSite> EnumerateMutations(
        LambdaExpressionSyntax lambda)
    {
        bool Descend(SyntaxNode node) =>
            ReferenceEquals(node, lambda) ||
            node is not (AnonymousFunctionExpressionSyntax or
                LocalFunctionStatementSyntax);

        foreach (var assignment in lambda.DescendantNodes(Descend)
                     .OfType<AssignmentExpressionSyntax>())
        {
            yield return new MutationSite(
                assignment,
                assignment.Left,
                assignment.OperatorToken.GetLocation());
        }

        foreach (var unary in lambda.DescendantNodes(Descend)
                     .OfType<PrefixUnaryExpressionSyntax>()
                     .Where(static expression => expression.IsKind(
                             SyntaxKind.PreIncrementExpression) ||
                         expression.IsKind(
                             SyntaxKind.PreDecrementExpression)))
        {
            yield return new MutationSite(
                unary,
                unary.Operand,
                unary.OperatorToken.GetLocation());
        }

        foreach (var unary in lambda.DescendantNodes(Descend)
                     .OfType<PostfixUnaryExpressionSyntax>()
                     .Where(static expression => expression.IsKind(
                             SyntaxKind.PostIncrementExpression) ||
                         expression.IsKind(
                             SyntaxKind.PostDecrementExpression)))
        {
            yield return new MutationSite(
                unary,
                unary.Operand,
                unary.OperatorToken.GetLocation());
        }

        foreach (var argument in lambda.DescendantNodes(Descend)
                     .OfType<ArgumentSyntax>()
                     .Where(static argument =>
                         argument.RefKindKeyword.IsKind(
                             SyntaxKind.RefKeyword) ||
                         argument.RefKindKeyword.IsKind(
                             SyntaxKind.OutKeyword)))
        {
            yield return new MutationSite(
                argument,
                argument.Expression,
                argument.RefKindKeyword.GetLocation());
        }
    }

    private static IEnumerable<UnsupportedSyntaxSite>
        EnumerateUnsupportedStatements(
            BlockSyntax block,
            LambdaExpressionSyntax lambda,
            ISet<TextSpanKey> readOnlyMutationSpans,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
    {
        foreach (var statement in block.Statements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (statement)
            {
                case LocalDeclarationStatementSyntax declaration:
                    if (!declaration.UsingKeyword.IsKind(SyntaxKind.None))
                    {
                        yield return new UnsupportedSyntaxSite(
                            "using local",
                            declaration.UsingKeyword.GetLocation());
                        continue;
                    }

                    if (declaration.Declaration.Type is RefTypeSyntax refType)
                    {
                        yield return new UnsupportedSyntaxSite(
                            "ref local",
                            refType.RefKeyword.GetLocation());
                        continue;
                    }

                    if (declaration.Declaration.Variables.Any(
                            static variable => variable.Initializer is null))
                    {
                        yield return new UnsupportedSyntaxSite(
                            "uninitialized local",
                            declaration.Declaration.Variables.First(
                                    static variable =>
                                        variable.Initializer is null)
                                .Identifier.GetLocation());
                    }

                    continue;

                case BlockSyntax nestedBlock:
                    foreach (var nested in EnumerateUnsupportedStatements(
                                 nestedBlock,
                                 lambda,
                                 readOnlyMutationSpans,
                                 semanticModel,
                                 cancellationToken))
                    {
                        yield return nested;
                    }

                    continue;

                case IfStatementSyntax ifStatement:
                    var constantCondition = GetBooleanConstant(
                        ifStatement.Condition,
                        semanticModel,
                        cancellationToken);

                    if (constantCondition != false)
                    {
                        foreach (var nested in EnumerateEmbeddedStatement(
                                     ifStatement.Statement,
                                     lambda,
                                     readOnlyMutationSpans,
                                     semanticModel,
                                     cancellationToken))
                        {
                            yield return nested;
                        }
                    }

                    if (constantCondition != true &&
                        ifStatement.Else is { } @else)
                    {
                        foreach (var nested in EnumerateEmbeddedStatement(
                                     @else.Statement,
                                     lambda,
                                     readOnlyMutationSpans,
                                     semanticModel,
                                     cancellationToken))
                        {
                            yield return nested;
                        }
                    }

                    continue;

                case SwitchStatementSyntax switchStatement:
                    foreach (var section in switchStatement.Sections)
                    {
                        var syntheticBlock = SyntaxFactory.Block(
                            section.Statements);

                        foreach (var nested in EnumerateUnsupportedStatements(
                                     syntheticBlock,
                                     lambda,
                                     readOnlyMutationSpans,
                                     semanticModel,
                                     cancellationToken))
                        {
                            yield return nested;
                        }
                    }

                    continue;

                case ReturnStatementSyntax:
                case ThrowStatementSyntax:
                    continue;

                case ExpressionStatementSyntax expressionStatement:
                    if (DeclarativeControlFlowPlanner
                            .TryBuildCompileTimeSourceDiscard(
                                expressionStatement,
                                semanticModel,
                                cancellationToken,
                                out _) ||
                        DeclarativeNestedMapExpression
                            .IsNestedUpdateStatement(
                                expressionStatement.Expression,
                                semanticModel,
                                cancellationToken))
                    {
                        continue;
                    }

                    if (readOnlyMutationSpans.Contains(
                            TextSpanKey.Create(
                                expressionStatement.Expression)))
                    {
                        continue;
                    }

                    yield return BuildExpressionStatementSite(
                        expressionStatement);
                    continue;

                case ForStatementSyntax forStatement:
                    yield return new UnsupportedSyntaxSite(
                        "for statement",
                        forStatement.ForKeyword.GetLocation());
                    continue;

                case ForEachStatementSyntax forEachStatement:
                    yield return new UnsupportedSyntaxSite(
                        "foreach statement",
                        forEachStatement.ForEachKeyword.GetLocation());
                    continue;

                case WhileStatementSyntax whileStatement:
                    yield return new UnsupportedSyntaxSite(
                        "while statement",
                        whileStatement.WhileKeyword.GetLocation());
                    continue;

                case DoStatementSyntax doStatement:
                    yield return new UnsupportedSyntaxSite(
                        "do statement",
                        doStatement.DoKeyword.GetLocation());
                    continue;

                case BreakStatementSyntax breakStatement:
                    yield return new UnsupportedSyntaxSite(
                        "break statement",
                        breakStatement.BreakKeyword.GetLocation());
                    continue;

                case ContinueStatementSyntax continueStatement:
                    yield return new UnsupportedSyntaxSite(
                        "continue statement",
                        continueStatement.ContinueKeyword.GetLocation());
                    continue;

                case LocalFunctionStatementSyntax localFunction:
                    yield return new UnsupportedSyntaxSite(
                        "local function",
                        localFunction.Identifier.GetLocation());
                    continue;

                case TryStatementSyntax tryStatement:
                    yield return new UnsupportedSyntaxSite(
                        "try statement",
                        tryStatement.TryKeyword.GetLocation());
                    continue;

                case UsingStatementSyntax usingStatement:
                    yield return new UnsupportedSyntaxSite(
                        "using statement",
                        usingStatement.UsingKeyword.GetLocation());
                    continue;

                case LockStatementSyntax lockStatement:
                    yield return new UnsupportedSyntaxSite(
                        "lock statement",
                        lockStatement.LockKeyword.GetLocation());
                    continue;

                case LabeledStatementSyntax labeledStatement:
                    yield return new UnsupportedSyntaxSite(
                        "labeled statement",
                        labeledStatement.Identifier.GetLocation());
                    continue;

                case GotoStatementSyntax gotoStatement:
                    yield return new UnsupportedSyntaxSite(
                        "goto statement",
                        gotoStatement.GotoKeyword.GetLocation());
                    continue;

                case UnsafeStatementSyntax unsafeStatement:
                    yield return new UnsupportedSyntaxSite(
                        "unsafe statement",
                        unsafeStatement.UnsafeKeyword.GetLocation());
                    continue;

                case FixedStatementSyntax fixedStatement:
                    yield return new UnsupportedSyntaxSite(
                        "fixed statement",
                        fixedStatement.FixedKeyword.GetLocation());
                    continue;

                case YieldStatementSyntax yieldStatement:
                    yield return new UnsupportedSyntaxSite(
                        "yield statement",
                        yieldStatement.YieldKeyword.GetLocation());
                    continue;

                default:
                    yield return new UnsupportedSyntaxSite(
                        StableSyntaxName(statement),
                        statement.GetLocation());
                    continue;
            }
        }
    }

    private static IEnumerable<UnsupportedSyntaxSite>
        EnumerateUnsupportedStructuredMutations(
            LambdaExpressionSyntax lambda,
            ISet<TextSpanKey> readOnlyMutationSpans,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
    {
        foreach (var mutation in EnumerateMutations(lambda))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (readOnlyMutationSpans.Contains(
                    TextSpanKey.Create(mutation.Node)) ||
                !IsStaticallyReachable(
                    mutation.Node,
                    lambda,
                    semanticModel,
                    cancellationToken) ||
                mutation.Node.AncestorsAndSelf()
                    .OfType<ExpressionStatementSyntax>()
                    .Any() ||
                HasUnsupportedOuterStatement(mutation.Node) ||
                mutation.Node is AssignmentExpressionSyntax
                {
                    Parent: InitializerExpressionSyntax
                } ||
                mutation.Node is ArgumentSyntax
                {
                    Expression: DeclarationExpressionSyntax
                })
            {
                continue;
            }

            yield return new UnsupportedSyntaxSite(
                MutationSyntaxName(mutation.Node),
                mutation.Location);
        }
    }

    private static bool HasUnsupportedOuterStatement(SyntaxNode node)
    {
        return node.Ancestors().OfType<StatementSyntax>().Any(statement =>
            statement is
                ForStatementSyntax or
                ForEachStatementSyntax or
                WhileStatementSyntax or
                DoStatementSyntax or
                TryStatementSyntax or
                UsingStatementSyntax or
                LockStatementSyntax or
                LabeledStatementSyntax or
                GotoStatementSyntax or
                UnsafeStatementSyntax or
                FixedStatementSyntax or
                LocalFunctionStatementSyntax or
                YieldStatementSyntax);
    }

    private static string MutationSyntaxName(SyntaxNode node)
    {
        return node switch
        {
            AssignmentExpressionSyntax assignment =>
                assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                    ? "assignment"
                    : "compound assignment",
            PrefixUnaryExpressionSyntax prefix =>
                prefix.IsKind(SyntaxKind.PreIncrementExpression)
                    ? "increment"
                    : "decrement",
            PostfixUnaryExpressionSyntax postfix =>
                postfix.IsKind(SyntaxKind.PostIncrementExpression)
                    ? "increment"
                    : "decrement",
            ArgumentSyntax argument =>
                argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
                    ? "ref argument"
                    : "out argument",
            _ => StableSyntaxName(node)
        };
    }

    private static IEnumerable<UnsupportedSyntaxSite>
        EnumerateEmbeddedStatement(
            StatementSyntax statement,
            LambdaExpressionSyntax lambda,
            ISet<TextSpanKey> readOnlyMutationSpans,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
    {
        var block = statement as BlockSyntax ?? SyntaxFactory.Block(statement);

        return EnumerateUnsupportedStatements(
            block,
            lambda,
            readOnlyMutationSpans,
            semanticModel,
            cancellationToken);
    }

    private static UnsupportedSyntaxSite BuildExpressionStatementSite(
        ExpressionStatementSyntax statement)
    {
        return statement.Expression switch
        {
            AssignmentExpressionSyntax assignment =>
                new UnsupportedSyntaxSite(
                    assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                        ? "assignment"
                        : "compound assignment",
                    assignment.OperatorToken.GetLocation()),
            PrefixUnaryExpressionSyntax prefix when prefix.IsKind(
                    SyntaxKind.PreIncrementExpression) ||
                prefix.IsKind(SyntaxKind.PreDecrementExpression) =>
                new UnsupportedSyntaxSite(
                    prefix.IsKind(SyntaxKind.PreIncrementExpression)
                        ? "increment"
                        : "decrement",
                    prefix.OperatorToken.GetLocation()),
            PostfixUnaryExpressionSyntax postfix when postfix.IsKind(
                    SyntaxKind.PostIncrementExpression) ||
                postfix.IsKind(SyntaxKind.PostDecrementExpression) =>
                new UnsupportedSyntaxSite(
                    postfix.IsKind(SyntaxKind.PostIncrementExpression)
                        ? "increment"
                        : "decrement",
                    postfix.OperatorToken.GetLocation()),
            AwaitExpressionSyntax awaitExpression =>
                new UnsupportedSyntaxSite(
                    "await expression",
                    awaitExpression.AwaitKeyword.GetLocation()),
            InvocationExpressionSyntax invocation =>
                new UnsupportedSyntaxSite(
                    "invocation statement",
                    invocation.GetLocation()),
            _ => new UnsupportedSyntaxSite(
                StableSyntaxName(statement.Expression),
                statement.Expression.GetLocation())
        };
    }

    private static string StableSyntaxName(SyntaxNode syntax)
    {
        var text = syntax.Kind().ToString();

        if (text.EndsWith("Statement", StringComparison.Ordinal))
        {
            text = text.Substring(
                0,
                text.Length - "Statement".Length) + " statement";
        }
        else if (text.EndsWith("Expression", StringComparison.Ordinal))
        {
            text = text.Substring(
                0,
                text.Length - "Expression".Length) + " expression";
        }

        return string.Concat(text.SelectMany((character, index) =>
                index > 0 && char.IsUpper(character)
                    ? new[] { ' ', char.ToLowerInvariant(character) }
                    : new[] { char.ToLowerInvariant(character) }))
            .Trim();
    }

    private readonly record struct CallbackModel(
        string Name,
        bool IsStructured,
        InvocationExpressionSyntax Invocation,
        BoundConfigurationExpression Expression,
        string Form);

    private readonly record struct CallbackAnalysisContext(
        string Name,
        bool IsStructured,
        string Form,
        InvocationExpressionSyntax Invocation,
        BoundConfigurationExpression Expression,
        string Contract,
        string PairKey,
        string MapperIdentity,
        string MapperDisplay,
        int LevelOrder,
        string CallbackOriginIdentity,
        bool IsDeclaringOrigin,
        MappingAnalysisContext MappingContext);

    private readonly record struct LambdaParameterRoles(
        IParameterSymbol? Source,
        IParameterSymbol? Previous,
        IParameterSymbol? Result,
        IParameterSymbol? Context);

    private readonly record struct MutationSite(
        SyntaxNode Node,
        ExpressionSyntax Target,
        Location Location);

    private readonly record struct UnsupportedSyntaxSite(
        string Name,
        Location Location);

    private readonly record struct TextSpanKey(int Start, int Length)
    {
        public static TextSpanKey Create(SyntaxNode node) =>
            new(node.SpanStart, node.Span.Length);
    }
}
