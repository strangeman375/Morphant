using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DeclarativeNestedMapExpression
{
    private const string MapMarkerMetadataName =
        "Morphant.Markers.MapMarker";

    private const string GenericMapMarkerMetadataName =
        "Morphant.Markers.MapMarker`1";

    private const string MemberMarkerMetadataName =
        "Morphant.Members.Member`1";

    public static bool TryBuild(
        ExpressionSyntax expression,
        ITypeSymbol? targetType,
        DeclarativeNestedMapTargetContext? targetContext,
        DeclarativeNestedMapUsageRegistry usageRegistry,
        IParameterSymbol sourceParameter,
        string? resultName,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken,
        out IReadOnlyDictionary<
            InvocationExpressionSyntax,
            TypeMapperNestedMapExpressionModel> mappings)
    {
        var result = new Dictionary<
            InvocationExpressionSyntax,
            TypeMapperNestedMapExpressionModel>(
            InvocationReferenceComparer.Instance);
        var semanticMapperType = semanticModel.Compilation
                .GetTypeByMetadataName(
                    SymbolNameHelper.GetFullMetadataName(mapperType)) ??
            mapperType;
        var mapperTypeSubstitutions =
            MapperTypeSubstitution.BuildForHierarchy(
                semanticMapperType);
        var effectiveTargetType = targetType is null
            ? null
            : MapperTypeSubstitution.Substitute(
                targetType,
                mapperTypeSubstitutions,
                semanticModel.Compilation);
        DeclarativeNestedMapTargetContext? effectiveTargetContext =
            targetContext is { } context
                ? context with
                {
                    Type = MapperTypeSubstitution.Substitute(
                        context.Type,
                        mapperTypeSubstitutions,
                        semanticModel.Compilation),
                    CurrentDestinationType =
                        context.CurrentDestinationType is { } currentType
                            ? MapperTypeSubstitution.Substitute(
                                currentType,
                                mapperTypeSubstitutions,
                                semanticModel.Compilation)
                            : null
                }
                : null;

        foreach (var invocation in expression
                     .DescendantNodesAndSelf()
                     .OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetMapMethod(
                    invocation,
                    semanticModel,
                    cancellationToken,
                    out var method))
            {
                continue;
            }

            if (!DeclarativeIntrinsic.HasSupportedTerminalPlacement(
                    expression,
                    invocation,
                    semanticModel,
                    cancellationToken) ||
                invocation.Ancestors()
                .OfType<InvocationExpressionSyntax>()
                .Any(ancestor =>
                    expression.Span.Contains(ancestor.Span) &&
                    TryGetMapMethod(
                        ancestor,
                        semanticModel,
                        cancellationToken,
                        out _)) ||
                !TryBuildMapping(
                    invocation,
                    expression,
                    method,
                    effectiveTargetType,
                    effectiveTargetContext,
                    usageRegistry,
                    sourceParameter,
                    resultName,
                    semanticModel,
                    mapperType,
                    mapperTypeSubstitutions,
                    cancellationToken,
                    out var mapping))
            {
                foreach (var observation in
                         BuildFailedObservations(
                        invocation,
                        expression,
                        method,
                        effectiveTargetType,
                        effectiveTargetContext,
                        usageRegistry,
                        sourceParameter,
                        resultName,
                        semanticModel,
                        mapperType,
                        cancellationToken))
                {
                    usageRegistry.Observe(observation);
                }

                mappings = ImmutableDictionary<
                    InvocationExpressionSyntax,
                    TypeMapperNestedMapExpressionModel>.Empty;
                return false;
            }

            result.Add(invocation, mapping);
        }

        var expressionType = semanticModel.GetTypeInfo(
                expression,
                cancellationToken)
            .Type;

        if (TryGetMarkerDestinationType(
                expressionType,
                out var markerDestination))
        {
            markerDestination = MapperTypeSubstitution.Substitute(
                markerDestination,
                mapperTypeSubstitutions,
                semanticModel.Compilation);

            if (effectiveTargetType is not null &&
                !HasWarningFreeImplicitConversion(
                    markerDestination,
                    effectiveTargetType,
                    semanticModel.Compilation,
                    mapperType,
                    cancellationToken))
            {
                mappings = ImmutableDictionary<
                    InvocationExpressionSyntax,
                    TypeMapperNestedMapExpressionModel>.Empty;
                return false;
            }
        }
        else if (IsNonGenericMapMarker(expressionType) &&
                 targetType is null &&
                 !result.Keys.Any(invocation =>
                     UnwrapParentheses(expression).Span.Equals(
                         invocation.Span)))
        {
            mappings = ImmutableDictionary<
                InvocationExpressionSyntax,
                TypeMapperNestedMapExpressionModel>.Empty;
            return false;
        }

        mappings = result;
        return true;
    }

    public static ITypeSymbol? GetEffectiveType(
        IOperation operation,
        ITypeSymbol? fallbackType,
        IReadOnlyDictionary<
            InvocationExpressionSyntax,
            TypeMapperNestedMapExpressionModel> mappings)
    {
        if (operation.Syntax is InvocationExpressionSyntax invocation &&
            mappings.TryGetValue(invocation, out var mapping))
        {
            return mapping.DestinationType;
        }

        if (DeclarativeIntrinsic.GetEffectiveValueType(operation) is
            { } valueType)
        {
            return valueType;
        }

        if (TryGetMarkerDestinationType(
                operation.Type,
                out var markerDestination))
        {
            return markerDestination;
        }

        if (IsNonGenericMapMarker(operation.Type))
        {
            return fallbackType;
        }

        return operation.Type ?? fallbackType;
    }

    public static bool TryGetMarkerDestinationType(
        ITypeSymbol? type,
        out ITypeSymbol destinationType)
    {
        if (type is INamedTypeSymbol
            {
                TypeArguments.Length: 1
            } namedType &&
            StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    namedType.OriginalDefinition),
                GenericMapMarkerMetadataName))
        {
            destinationType = namedType.TypeArguments[0]
                .WithNullableAnnotation(
                    namedType.TypeArgumentNullableAnnotations[0]);
            return true;
        }

        destinationType = null!;
        return false;
    }

    public static bool IsMapMarkerType(ITypeSymbol? type)
    {
        return TryGetMarkerDestinationType(type, out _) ||
               IsNonGenericMapMarker(type);
    }

    private static bool TryGetMemberMarkerDestinationType(
        ITypeSymbol? type,
        out ITypeSymbol destinationType)
    {
        if (type is INamedTypeSymbol
            {
                TypeArguments.Length: 1
            } namedType &&
            StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    namedType.OriginalDefinition),
                MemberMarkerMetadataName))
        {
            destinationType = namedType.TypeArguments[0]
                .WithNullableAnnotation(
                    namedType.TypeArgumentNullableAnnotations[0]);
            return true;
        }

        destinationType = null!;
        return false;
    }

    public static bool IsReadOnlyMemberUpdateStatement(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        expression = UnwrapParentheses(expression);

        if (expression is not InvocationExpressionSyntax invocation ||
            !TryGetMapMethod(
                invocation,
                semanticModel,
                cancellationToken,
                out var method) ||
            method.Name != "Update" ||
            semanticModel.GetOperation(
                invocation,
                cancellationToken) is not IInvocationOperation operation)
        {
            return false;
        }

        return TryBuildReadOnlyMemberUpdateTarget(
            invocation,
            operation,
            resultName: "result",
            semanticModel,
            cancellationToken,
            out _);
    }

    public static bool IsNestedUpdateStatement(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        expression = UnwrapParentheses(expression);

        return expression is InvocationExpressionSyntax invocation &&
               invocation.Parent is ExpressionStatementSyntax &&
               TryGetMapMethod(
                   invocation,
                   semanticModel,
                   cancellationToken,
                   out var method) &&
               method.Name == "Update";
    }

    private static bool TryBuildMapping(
        InvocationExpressionSyntax invocation,
        SyntaxNode terminalTarget,
        IMethodSymbol method,
        ITypeSymbol? targetType,
        DeclarativeNestedMapTargetContext? targetContext,
        DeclarativeNestedMapUsageRegistry usageRegistry,
        IParameterSymbol sourceParameter,
        string? resultName,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol>
            mapperTypeSubstitutions,
        CancellationToken cancellationToken,
        out TypeMapperNestedMapExpressionModel mapping)
    {
        var operation = semanticModel.GetOperation(
            invocation,
            cancellationToken) as IInvocationOperation;

        if (operation is null ||
            !TryGetOperation(method, targetContext, out var nestedOperation))
        {
            mapping = default;
            return false;
        }

        var sourceArgument = operation.Arguments.FirstOrDefault(argument =>
            argument.Parameter?.Name == "source")?.Syntax as ArgumentSyntax;
        ITypeSymbol? sourceType;
        string? inferredSourceMemberName = null;

        if (sourceArgument is not null)
        {
            var sourceTypeInfo = semanticModel.GetTypeInfo(
                sourceArgument.Expression,
                cancellationToken);
            sourceType = sourceArgument.Expression.IsKind(
                    SyntaxKind.DefaultLiteralExpression)
                ? null
                : sourceTypeInfo.Type?.WithNullableAnnotation(
                    sourceTypeInfo.Nullability.Annotation);
        }
        else if (method.Name == "Map" &&
                 targetContext is { } inferredTarget &&
                 TryFindInferredSourceMember(
                     inferredTarget.SourceMemberName,
                     sourceParameter,
                     semanticModel,
                     mapperType,
                     cancellationToken) is { } sourceMember)
        {
            sourceType = sourceMember.Type;
            inferredSourceMemberName = sourceMember.Name;
        }
        else
        {
            sourceType = null;
        }

        if (sourceType is not null)
        {
            sourceType = MapperTypeSubstitution.Substitute(
                sourceType,
                mapperTypeSubstitutions,
                semanticModel.Compilation);
        }

        if (sourceType is null ||
            !CanUseAsGenericArgument(sourceType))
        {
            mapping = default;
            return false;
        }

        ITypeSymbol? destinationType;
        var readOnlyTarget = default(ReadOnlyMemberUpdateTarget?);

        if (nestedOperation == DeclarativeNestedMapOperation.Update &&
            TryBuildReadOnlyMemberUpdateTarget(
                invocation,
                operation,
                resultName,
                semanticModel,
                cancellationToken,
                out var resolvedReadOnlyTarget))
        {
            readOnlyTarget = resolvedReadOnlyTarget with
            {
                MemberType = MapperTypeSubstitution.Substitute(
                    resolvedReadOnlyTarget.MemberType,
                    mapperTypeSubstitutions,
                    semanticModel.Compilation)
            };
        }

        if (method.IsGenericMethod && method.TypeArguments.Length == 1)
        {
            destinationType = method.TypeArguments[0]
                .WithNullableAnnotation(
                    method.TypeArgumentNullableAnnotations[0]);
        }
        else if (readOnlyTarget is { } inferredReadOnlyTarget)
        {
            destinationType = inferredReadOnlyTarget.MemberType;
        }
        else
        {
            destinationType = targetType;
        }

        if (destinationType is not null)
        {
            destinationType = MapperTypeSubstitution.Substitute(
                destinationType,
                mapperTypeSubstitutions,
                semanticModel.Compilation);
        }

        if (destinationType is null ||
            !CanUseAsGenericArgument(destinationType) ||
            targetType is not null &&
            !HasWarningFreeImplicitConversion(
                destinationType,
                targetType,
                semanticModel.Compilation,
                mapperType,
                cancellationToken) ||
            nestedOperation == DeclarativeNestedMapOperation.Update &&
            readOnlyTarget is null &&
            method.Name == "Update" &&
            !HasCompatibleDestinationArgument(
                operation,
                destinationType,
                semanticModel,
                mapperType,
                mapperTypeSubstitutions,
                cancellationToken))
        {
            mapping = default;
            return false;
        }

        string? generatedDestinationExpression = null;
        ITypeSymbol? generatedDestinationType = null;
        var guardNullDestination = false;
        string? guardVariableName = null;
        string? compatibleDestinationName = null;
        string? incompatibleDestinationName = null;

        if (readOnlyTarget is { } readOnly)
        {
            if (!readOnly.MemberType.IsReferenceType ||
                !HasRuntimeDestinationConversion(
                    readOnly.MemberType,
                    destinationType,
                    semanticModel.Compilation,
                    mapperType,
                    cancellationToken))
            {
                mapping = default;
                return false;
            }

            generatedDestinationExpression = readOnly.Expression;
            generatedDestinationType = readOnly.MemberType;
            guardNullDestination = true;
            guardVariableName = usageRegistry.AllocateGuardName(
                "destination" + readOnly.MemberName,
                mapperType);
        }
        else if (nestedOperation == DeclarativeNestedMapOperation.Update &&
                 method.Name == "Map")
        {
            var adaptiveCurrentType = targetContext?
                .CurrentDestinationType ?? targetContext?.Type;

            if (targetContext is not
                    {
                        CurrentDestinationExpression: { } currentDestination
                    } adaptiveTarget ||
                adaptiveCurrentType is null ||
                !HasRuntimeDestinationConversion(
                    adaptiveCurrentType,
                    destinationType,
                    semanticModel.Compilation,
                    mapperType,
                    cancellationToken) ||
                !usageRegistry.TryRegisterAdaptiveUpdate(
                    invocation,
                    currentDestination,
                    targetContext?.TargetDesignator))
            {
                mapping = default;
                return false;
            }

            generatedDestinationExpression = currentDestination;
            generatedDestinationType = adaptiveCurrentType;
        }

        if (generatedDestinationType is { } currentDestinationType &&
            !SymbolEqualityComparer.IncludeNullability.Equals(
                currentDestinationType,
                destinationType))
        {
            compatibleDestinationName = usageRegistry.AllocateGuardName(
                "nestedDestination",
                mapperType);
            incompatibleDestinationName = usageRegistry.AllocateGuardName(
                "incompatibleDestination",
                mapperType);
        }

        mapping = new TypeMapperNestedMapExpressionModel(
            sourceType,
            destinationType,
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(sourceType),
            TypeMapperMappingTypePolicy.GetGeneratedRuntimeTypeName(
                sourceType),
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                destinationType),
            nestedOperation,
            inferredSourceMemberName,
            generatedDestinationExpression,
            generatedDestinationType,
            guardNullDestination,
            guardVariableName,
            TypeMapperMappingTypePolicy.GetGeneratedRuntimeTypeName(
                MappingTypeNormalization.NormalizePreviousDestination(
                    destinationType,
                    semanticModel.Compilation)),
            compatibleDestinationName,
            incompatibleDestinationName,
            new NestedMappingObservation(
                invocation,
                method,
                terminalTarget,
                nestedOperation,
                sourceType,
                destinationType,
                NestedConversionStatus.Compatible,
                readOnlyTarget is not null
                    ? NestedDestinationOrigin.ReadOnlyProxy
                    : generatedDestinationExpression is not null
                        ? NestedDestinationOrigin.GeneratedCurrent
                        : operation.Arguments.Any(argument =>
                            argument.Parameter?.Name == "destination")
                            ? NestedDestinationOrigin.Explicit
                            : NestedDestinationOrigin.None,
                operation.Arguments.FirstOrDefault(argument =>
                        argument.Parameter?.Name == "destination")
                    ?.Syntax is ArgumentSyntax destinationArgument
                        ? destinationArgument.Expression
                        : null,
                operation.Arguments.FirstOrDefault(argument =>
                        argument.Parameter?.Name == "destination")
                    ?.Value.Type,
                generatedDestinationExpression,
                readOnlyTarget?.Member,
                usageRegistry.GetAdaptiveTargets(invocation),
                usageRegistry.GetAdaptiveTargetDesignators(invocation),
                NestedMappingFailureKind.None,
                sourceArgument?.Expression,
                targetType,
                targetContext?.SourceMemberName,
                targetContext?.TargetSymbol,
                targetContext?.TargetDesignator,
                readOnlyTarget?.MemberType ??
                targetContext?.CurrentDestinationType,
                readOnlyTarget?.Member ??
                targetContext?.CurrentDestinationSymbol,
                GetSourceMapper(invocation, semanticModel, mapperType),
                targetContext?.Paths ?? usageRegistry.Paths));
        usageRegistry.Observe(mapping.Observation);
        return true;
    }

    private static ImmutableArray<NestedMappingObservation>
        BuildFailedObservations(
        InvocationExpressionSyntax invocation,
        SyntaxNode terminalTarget,
        IMethodSymbol method,
        ITypeSymbol? targetType,
        DeclarativeNestedMapTargetContext? targetContext,
        DeclarativeNestedMapUsageRegistry usageRegistry,
        IParameterSymbol sourceParameter,
        string? resultName,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        var operation = semanticModel.GetOperation(
            invocation,
            cancellationToken) as IInvocationOperation;
        var sourceArgument = operation?.Arguments.FirstOrDefault(argument =>
            argument.Parameter?.Name == "source")?.Syntax as ArgumentSyntax;
        var mapperTypeSubstitutions =
            MapperTypeSubstitution.BuildForHierarchy(mapperType);
        var sourceTypeInfo = sourceArgument is null
            ? default
            : semanticModel.GetTypeInfo(
                sourceArgument.Expression,
                cancellationToken);
        var sourceType = sourceArgument?.Expression.IsKind(
                SyntaxKind.DefaultLiteralExpression) == true
            ? null
            : sourceTypeInfo.Type?.WithNullableAnnotation(
                sourceTypeInfo.Nullability.Annotation);

        if (sourceType is null &&
            sourceArgument is null &&
            method.Name == "Map" &&
            targetContext is { } inferredTarget &&
            TryFindInferredSourceMember(
                inferredTarget.SourceMemberName,
                sourceParameter,
                semanticModel,
                mapperType,
                cancellationToken) is { } sourceMember)
        {
            sourceType = sourceMember.Type;
        }

        if (sourceType is not null)
        {
            sourceType = MapperTypeSubstitution.Substitute(
                sourceType,
                mapperTypeSubstitutions,
                semanticModel.Compilation);
        }

        var destinationType = method.IsGenericMethod &&
            method.TypeArguments.Length == 1
                ? method.TypeArguments[0]
                : targetType;
        var nestedOperation = TryGetOperation(
                method,
                targetContext,
                out var resolvedOperation)
            ? resolvedOperation
            : (DeclarativeNestedMapOperation?)null;
        var explicitDestination = operation?.Arguments.FirstOrDefault(
                argument => argument.Parameter?.Name == "destination")
            ?.Syntax is ArgumentSyntax destinationArgument
                ? destinationArgument.Expression
                : null;
        var readOnlyTarget = default(ReadOnlyMemberUpdateTarget);
        var hasReadOnlyProxy = operation is not null &&
            TryBuildReadOnlyMemberUpdateTarget(
                invocation,
                operation,
                resultName,
                semanticModel,
                cancellationToken,
                out readOnlyTarget);

        if (destinationType is null &&
            explicitDestination is not null &&
            TryGetMemberMarkerDestinationType(
                semanticModel.GetTypeInfo(
                        explicitDestination,
                        cancellationToken)
                    .Type,
                out var proxyDestinationType))
        {
            destinationType = proxyDestinationType;
        }

        if (destinationType is not null)
        {
            destinationType = MapperTypeSubstitution.Substitute(
                destinationType,
                mapperTypeSubstitutions,
                semanticModel.Compilation);
        }

        var currentDestinationType = hasReadOnlyProxy
            ? readOnlyTarget.MemberType
            : targetContext?.CurrentDestinationType ?? targetContext?.Type;

        if (currentDestinationType is not null)
        {
            currentDestinationType = MapperTypeSubstitution.Substitute(
                currentDestinationType,
                mapperTypeSubstitutions,
                semanticModel.Compilation);
        }

        var resultConversion = destinationType is null
            ? NestedConversionStatus.Unknown
            : targetType is null ||
              HasWarningFreeImplicitConversion(
                  destinationType,
                  targetType,
                  semanticModel.Compilation,
                  mapperType,
                  cancellationToken)
                ? NestedConversionStatus.Compatible
                : NestedConversionStatus.Incompatible;
        var adaptiveTargets = usageRegistry.GetAdaptiveTargets(invocation);
        var standaloneUpdate = method.Name == "Update" &&
            invocation.Parent is ExpressionStatementSyntax;
        var failureKind = ClassifyFailure(
            method,
            sourceArgument,
            sourceType,
            destinationType,
            resultConversion,
            nestedOperation,
            explicitDestination,
            standaloneUpdate,
            hasReadOnlyProxy,
            readOnlyTarget,
            targetContext,
            currentDestinationType,
            adaptiveTargets,
            operation,
            semanticModel,
            mapperType,
            mapperTypeSubstitutions,
            cancellationToken);

        var observation = new NestedMappingObservation(
            invocation,
            method,
            terminalTarget,
            nestedOperation,
            sourceType,
            destinationType,
            resultConversion,
            hasReadOnlyProxy
                ? NestedDestinationOrigin.ReadOnlyProxy
                : explicitDestination is not null
                    ? NestedDestinationOrigin.Explicit
                    : targetContext?.CurrentDestinationExpression is not null
                        ? NestedDestinationOrigin.GeneratedCurrent
                        : NestedDestinationOrigin.None,
            explicitDestination,
            explicitDestination is null
                ? null
                : semanticModel.GetTypeInfo(
                        explicitDestination,
                        cancellationToken)
                    .Type,
            targetContext?.CurrentDestinationExpression,
            hasReadOnlyProxy ? readOnlyTarget.Member : null,
            adaptiveTargets,
            usageRegistry.GetAdaptiveTargetDesignators(invocation),
            failureKind,
            sourceArgument?.Expression,
            targetType,
            targetContext?.SourceMemberName,
            targetContext?.TargetSymbol,
            targetContext?.TargetDesignator,
            currentDestinationType,
            hasReadOnlyProxy
                ? readOnlyTarget.Member
                : targetContext?.CurrentDestinationSymbol,
            GetSourceMapper(invocation, semanticModel, mapperType),
            targetContext?.Paths ?? usageRegistry.Paths);
        var result = ImmutableArray.CreateBuilder<
            NestedMappingObservation>();
        result.Add(observation);

        if (failureKind == NestedMappingFailureKind.ResultIncompatible &&
            nestedOperation == DeclarativeNestedMapOperation.Update &&
            explicitDestination is not null &&
            destinationType is not null &&
            method.Name == "Update")
        {
            var explicitFailure = ClassifyExplicitDestinationFailure(
                explicitDestination,
                destinationType,
                operation,
                semanticModel,
                mapperType,
                mapperTypeSubstitutions,
                cancellationToken);

            if (explicitFailure != NestedMappingFailureKind.None)
            {
                result.Add(observation with
                {
                    FailureKind = explicitFailure
                });
            }
        }

        return result.ToImmutable();
    }

    private static INamedTypeSymbol GetSourceMapper(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        INamedTypeSymbol fallback)
    {
        return semanticModel.GetEnclosingSymbol(invocation.SpanStart)
                   ?.ContainingType ??
               fallback;
    }

    private static NestedMappingFailureKind ClassifyFailure(
        IMethodSymbol method,
        ArgumentSyntax? sourceArgument,
        ITypeSymbol? sourceType,
        ITypeSymbol? destinationType,
        NestedConversionStatus resultConversion,
        DeclarativeNestedMapOperation? operation,
        ExpressionSyntax? explicitDestination,
        bool standaloneUpdate,
        bool hasReadOnlyProxy,
        ReadOnlyMemberUpdateTarget readOnlyTarget,
        DeclarativeNestedMapTargetContext? targetContext,
        ITypeSymbol? currentDestinationType,
        ImmutableArray<string> adaptiveTargets,
        IInvocationOperation? invocationOperation,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol>
            mapperTypeSubstitutions,
        CancellationToken cancellationToken)
    {
        if (sourceType is null)
        {
            return sourceArgument is null && method.Name == "Map"
                ? NestedMappingFailureKind.ParameterlessSourceUnavailable
                : NestedMappingFailureKind.SourceTypeUnknown;
        }

        if (destinationType is null)
        {
            return standaloneUpdate
                ? NestedMappingFailureKind.ReadOnlyProxyInvalid
                : NestedMappingFailureKind.DestinationTypeUnknown;
        }

        if (resultConversion == NestedConversionStatus.Incompatible)
        {
            return NestedMappingFailureKind.ResultIncompatible;
        }

        if (operation != DeclarativeNestedMapOperation.Update)
        {
            return NestedMappingFailureKind.None;
        }

        if (standaloneUpdate && !hasReadOnlyProxy)
        {
            return NestedMappingFailureKind.ReadOnlyProxyInvalid;
        }

        if (hasReadOnlyProxy)
        {
            return !readOnlyTarget.MemberType.IsReferenceType ||
                   !HasRuntimeDestinationConversion(
                       readOnlyTarget.MemberType,
                       destinationType,
                       semanticModel.Compilation,
                       mapperType,
                       cancellationToken)
                ? NestedMappingFailureKind.AdaptiveCurrentIncompatible
                : NestedMappingFailureKind.None;
        }

        if (explicitDestination is not null && method.Name == "Update")
        {
            return ClassifyExplicitDestinationFailure(
                explicitDestination,
                destinationType,
                invocationOperation,
                semanticModel,
                mapperType,
                mapperTypeSubstitutions,
                cancellationToken);
        }

        if (method.Name != "Map")
        {
            return NestedMappingFailureKind.None;
        }

        if (targetContext?.CurrentDestinationExpression is null)
        {
            return NestedMappingFailureKind.AdaptiveCurrentUnavailable;
        }

        if (currentDestinationType is null ||
            !HasRuntimeDestinationConversion(
                currentDestinationType,
                destinationType,
                semanticModel.Compilation,
                mapperType,
                cancellationToken))
        {
            return NestedMappingFailureKind.AdaptiveCurrentIncompatible;
        }

        return adaptiveTargets.Length > 1
            ? NestedMappingFailureKind.AdaptiveCurrentAmbiguous
            : NestedMappingFailureKind.None;
    }

    private static NestedMappingFailureKind
        ClassifyExplicitDestinationFailure(
            ExpressionSyntax explicitDestination,
            ITypeSymbol destinationType,
            IInvocationOperation? invocationOperation,
            SemanticModel semanticModel,
            INamedTypeSymbol mapperType,
            IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol>
                mapperTypeSubstitutions,
            CancellationToken cancellationToken)
    {
        if (explicitDestination.IsKind(SyntaxKind.NullLiteralExpression) &&
            destinationType.IsValueType &&
            destinationType is not INamedTypeSymbol
            {
                OriginalDefinition.SpecialType:
                    SpecialType.System_Nullable_T
            })
        {
            return NestedMappingFailureKind
                .ExplicitNullForNonNullableValue;
        }

        return invocationOperation is null ||
               !HasCompatibleDestinationArgument(
                   invocationOperation,
                   destinationType,
                   semanticModel,
                   mapperType,
                   mapperTypeSubstitutions,
                   cancellationToken)
            ? NestedMappingFailureKind.ExplicitDestinationIncompatible
            : NestedMappingFailureKind.None;
    }

    private static bool HasCompatibleDestinationArgument(
        IInvocationOperation operation,
        ITypeSymbol destinationType,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol>
            mapperTypeSubstitutions,
        CancellationToken cancellationToken)
    {
        if (operation.Arguments.FirstOrDefault(argument =>
                argument.Parameter?.Name == "destination") is not
            { Syntax: ArgumentSyntax destinationArgument })
        {
            return false;
        }

        var expression = destinationArgument.Expression;

        if (expression.IsKind(SyntaxKind.DefaultLiteralExpression))
        {
            return true;
        }

        if (expression.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return destinationType.IsReferenceType ||
                   destinationType is INamedTypeSymbol namedType &&
                   namedType.OriginalDefinition.SpecialType ==
                       SpecialType.System_Nullable_T;
        }

        var argumentType = semanticModel.GetTypeInfo(
                expression,
                cancellationToken)
            .Type;

        if (argumentType is null)
        {
            return false;
        }

        argumentType = MapperTypeSubstitution.Substitute(
            argumentType,
            mapperTypeSubstitutions,
            semanticModel.Compilation);

        var inputType = destinationType.IsReferenceType
            ? destinationType.WithNullableAnnotation(
                NullableAnnotation.Annotated)
            : destinationType;

        return HasWarningFreeImplicitConversion(
            argumentType,
            inputType,
            semanticModel.Compilation,
            mapperType,
            cancellationToken);
    }

    private static bool TryGetOperation(
        IMethodSymbol method,
        DeclarativeNestedMapTargetContext? targetContext,
        out DeclarativeNestedMapOperation operation)
    {
        switch (method.Name)
        {
            case "Map" when method.Parameters.Length is 0 or 1 &&
                                 targetContext is { } target:
                operation = target.Operation;
                return true;

            case "Create" when method.Parameters.Length == 1:
                operation = DeclarativeNestedMapOperation.Create;
                return true;

            case "Update" when method.Parameters.Length == 2:
                operation = DeclarativeNestedMapOperation.Update;
                return true;

            default:
                operation = default;
                return false;
        }
    }

    private static ConventionReadableMember? TryFindInferredSourceMember(
        string memberName,
        IParameterSymbol sourceParameter,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (semanticModel.Compilation is not
            CSharpCompilation compilation)
        {
            return null;
        }

        var accessibleWithin = compilation.GetTypeByMetadataName(
                SymbolNameHelper.GetFullMetadataName(
                    mapperType.OriginalDefinition)) ??
            mapperType;

        return ConventionMemberMappingPlanner.BuildReadableMembers(
                sourceParameter.Type,
                compilation,
                accessibleWithin,
                cancellationToken)
            .FirstOrDefault(member => StringComparer.Ordinal.Equals(
                member.Name,
                memberName));
    }

    private static bool TryBuildReadOnlyMemberUpdateTarget(
        InvocationExpressionSyntax invocation,
        IInvocationOperation operation,
        string? resultName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ReadOnlyMemberUpdateTarget target)
    {
        if (resultName is null ||
            invocation.Parent is not ExpressionStatementSyntax ||
            operation.Arguments.FirstOrDefault(argument =>
                argument.Parameter?.Name == "destination") is not
                { Syntax: ArgumentSyntax destinationArgument } ||
            UnwrapParentheses(destinationArgument.Expression) is not
                MemberAccessExpressionSyntax memberAccess ||
            UnwrapParentheses(memberAccess.Expression) is not
                IdentifierNameSyntax receiver ||
            semanticModel.GetSymbolInfo(
                    receiver,
                    cancellationToken)
                .Symbol is not ILocalSymbol receiverLocal ||
            !IsDeclarativeResultLocal(
                invocation,
                receiverLocal,
                semanticModel,
                cancellationToken) ||
            semanticModel.GetSymbolInfo(
                    memberAccess,
                    cancellationToken)
                .Symbol is not IPropertySymbol
                {
                    GetMethod: not null,
                    SetMethod: null,
                    Type: INamedTypeSymbol
                    {
                        TypeArguments.Length: 1
                    } memberMarkerType
                } property ||
            !StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    memberMarkerType.OriginalDefinition),
                MemberMarkerMetadataName))
        {
            target = default;
            return false;
        }

        var memberType = memberMarkerType.TypeArguments[0]
            .WithNullableAnnotation(
                memberMarkerType.TypeArgumentNullableAnnotations[0]);
        target = new ReadOnlyMemberUpdateTarget(
            property.Name,
            memberType,
            resultName + "." + Identifier(property.Name),
            property);
        return true;
    }

    private static bool IsDeclarativeResultLocal(
        InvocationExpressionSyntax invocation,
        ILocalSymbol local,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var lambda = invocation.Ancestors()
            .OfType<LambdaExpressionSyntax>()
            .FirstOrDefault();
        var resultType = lambda is null
            ? null
            : (semanticModel.GetTypeInfo(
                    lambda,
                    cancellationToken)
                .ConvertedType as INamedTypeSymbol)?
                .DelegateInvokeMethod?
                .ReturnType;

        return resultType is not null &&
               SymbolEqualityComparer.Default.Equals(
                   local.Type,
                   resultType);
    }

    private static bool HasRuntimeDestinationConversion(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        Compilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (compilation is not CSharpCompilation csharpCompilation)
        {
            return false;
        }

        var containment = csharpCompilation.ClassifyConversion(
            destinationType,
            sourceType);
        var exactNullableSlot = sourceType is INamedTypeSymbol
            {
                OriginalDefinition.SpecialType:
                    SpecialType.System_Nullable_T,
                TypeArguments.Length: 1
            } nullableSlot &&
            SymbolEqualityComparer.IncludeNullability.Equals(
                nullableSlot.TypeArguments[0],
                destinationType);

        if ((containment.IsIdentity ||
             containment.IsImplicit &&
             (containment.IsReference || containment.IsBoxing) ||
             exactNullableSlot) &&
            HasWarningFreeImplicitConversion(
                destinationType,
                sourceType,
                compilation,
                mapperType,
                cancellationToken))
        {
            return true;
        }

        if (sourceType.IsValueType || destinationType.IsValueType)
        {
            return false;
        }

        var usedNames = UserResultMappingPlanner.BuildUsedLocalNames(
            mapperType);
        var methodName = UserResultMappingPlanner.AllocateName(
            "__MorphantBindNestedUpdateDestination",
            usedNames);
        var sourceTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(sourceType);
        var destinationTypeName = BuildMaybeNullTypeName(
            destinationType);
        var probeTree = MapperProbeSyntax.Build(
            mapperType,
            "Morphant.NestedUpdateDestinationProbe.g.cs",
            writer =>
            {
                writer.OpenBlock(
                    $"private void {methodName}({sourceTypeName} value)");
                writer.Line($"_ = ({destinationTypeName})value;");
                writer.CloseBlock();
            });
        var diagnostics = csharpCompilation
            .WithOptions(
                csharpCompilation.Options
                    .WithReportSuppressedDiagnostics(true))
            .AddSyntaxTrees(probeTree)
            .GetSemanticModel(probeTree)
            .GetDiagnostics(cancellationToken: cancellationToken);

        return diagnostics.All(diagnostic =>
            diagnostic.DefaultSeverity != DiagnosticSeverity.Error);
    }

    private static string BuildMaybeNullTypeName(ITypeSymbol type)
    {
        var typeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(type);

        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType ==
                SpecialType.System_Nullable_T)
        {
            return typeName;
        }

        if (type.IsValueType)
        {
            return typeName;
        }

        return TypeMapperMappingTypePolicy.GetGeneratedTypeName(
            type.WithNullableAnnotation(NullableAnnotation.Annotated));
    }

    private static ExpressionSyntax UnwrapParentheses(
        ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized.Expression;
        }

        return expression;
    }

    private static bool HasWarningFreeImplicitConversion(
        ITypeSymbol sourceType,
        ITypeSymbol destinationType,
        Compilation compilation,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (compilation is not CSharpCompilation csharpCompilation)
        {
            return false;
        }

        var usedNames = UserResultMappingPlanner.BuildUsedLocalNames(
            mapperType);
        var methodName = UserResultMappingPlanner.AllocateName(
            "__MorphantBindNestedMapConversion",
            usedNames);
        var sourceTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(sourceType);
        var destinationTypeName =
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                destinationType);
        var probeTree = MapperProbeSyntax.Build(
            mapperType,
            "Morphant.NestedMapConversionProbe.g.cs",
            writer => writer.Line(
                $"private {destinationTypeName} {methodName}(" +
                $"{sourceTypeName} value) => value;"));
        var probeCompilation = csharpCompilation
            .WithOptions(
                csharpCompilation.Options
                    .WithReportSuppressedDiagnostics(true))
            .AddSyntaxTrees(probeTree);
        var diagnostics = probeCompilation
            .GetSemanticModel(probeTree)
            .GetDiagnostics(cancellationToken: cancellationToken);

        return diagnostics.All(diagnostic =>
            diagnostic.Severity is not
                (DiagnosticSeverity.Warning or DiagnosticSeverity.Error));
    }

    private static bool CanUseAsGenericArgument(ITypeSymbol type)
    {
        if (type.TypeKind is
                TypeKind.Error or
                TypeKind.Pointer or
                TypeKind.FunctionPointer ||
            type.SpecialType == SpecialType.System_Void ||
            type.IsRefLikeType)
        {
            return false;
        }

        if (type is IDynamicTypeSymbol or ITypeParameterSymbol)
        {
            return true;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return CanUseAsGenericArgument(arrayType.ElementType);
        }

        if (type is not INamedTypeSymbol namedType ||
            namedType.IsAnonymousType ||
            namedType.IsFileLocal ||
            namedType.IsStatic ||
            namedType.IsUnboundGenericType ||
            !namedType.CanBeReferencedByName)
        {
            return false;
        }

        return namedType.TypeArguments.All(typeArgument =>
            CanUseAsGenericArgument(typeArgument));
    }

    private static bool TryGetMapMethod(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out IMethodSymbol method)
    {
        if (!DeclarativeIntrinsic.TryGetKind(
                invocation,
                semanticModel,
                cancellationToken,
                out var kind,
                out _) ||
            kind is not (
                DeclarativeIntrinsicKind.Map or
                DeclarativeIntrinsicKind.Create or
                DeclarativeIntrinsicKind.Update) ||
            semanticModel.GetSymbolInfo(
                    invocation,
                    cancellationToken)
                .Symbol is not IMethodSymbol boundMethod)
        {
            method = null!;
            return false;
        }

        method = boundMethod;
        return true;
    }

    private static bool IsNonGenericMapMarker(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol namedType &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       namedType.OriginalDefinition),
                   MapMarkerMetadataName);
    }

    private static string Identifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) != SyntaxKind.None ||
               SyntaxFacts.GetContextualKeywordKind(value) != SyntaxKind.None
            ? "@" + value
            : value;
    }

    internal sealed class InvocationReferenceComparer
        : IEqualityComparer<InvocationExpressionSyntax>
    {
        public static InvocationReferenceComparer Instance { get; } = new();

        public bool Equals(
            InvocationExpressionSyntax? left,
            InvocationExpressionSyntax? right) =>
            ReferenceEquals(left, right);

        public int GetHashCode(InvocationExpressionSyntax value) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
    }
}

internal readonly record struct TypeMapperNestedMapExpressionModel
(
    ITypeSymbol SourceType,
    ITypeSymbol DestinationType,
    string SourceTypeName,
    string RuntimeSourceTypeName,
    string DestinationTypeName,
    DeclarativeNestedMapOperation Operation,
    string? InferredSourceMemberName,
    string? GeneratedDestinationExpression,
    ITypeSymbol? GeneratedDestinationType,
    bool GuardNullDestination,
    string? GuardVariableName,
    string RuntimeDestinationTypeName,
    string? CompatibleDestinationName,
    string? IncompatibleDestinationName,
    NestedMappingObservation Observation
);

internal enum DeclarativeNestedMapOperation
{
    Create,
    Update
}

internal readonly record struct DeclarativeNestedMapTargetContext(
    ITypeSymbol Type,
    string SourceMemberName,
    DeclarativeNestedMapOperation Operation,
    string? CurrentDestinationExpression,
    ITypeSymbol? CurrentDestinationType,
    ISymbol? TargetSymbol,
    SyntaxNode? TargetDesignator,
    ISymbol? CurrentDestinationSymbol,
    MappingExecutionPathSet Paths);

internal sealed class DeclarativeNestedMapUsageRegistry
{
    private readonly Dictionary<InvocationExpressionSyntax, string>
        _adaptiveUpdateTargets = new(
            DeclarativeNestedMapExpression
                .InvocationReferenceComparer.Instance);
    private readonly Dictionary<InvocationExpressionSyntax, List<string>>
        _adaptiveUpdateTargetUses = new(
            DeclarativeNestedMapExpression
                .InvocationReferenceComparer.Instance);
    private readonly Dictionary<InvocationExpressionSyntax, List<SyntaxNode>>
        _adaptiveUpdateTargetDesignators = new(
            DeclarativeNestedMapExpression
                .InvocationReferenceComparer.Instance);
    private readonly HashSet<string> _usedGuardNames =
        new(StringComparer.Ordinal);
    private readonly List<NestedMappingObservation> _observations = [];

    public DeclarativeNestedMapUsageRegistry(
        MappingExecutionPathSet paths = MappingExecutionPathSet.All)
    {
        Paths = paths;
    }

    public MappingExecutionPathSet Paths { get; }

    public ImmutableArray<NestedMappingObservation> Observations =>
        _observations.ToImmutableArray();

    public void Observe(NestedMappingObservation observation)
    {
        _observations.Add(observation);
    }

    public ImmutableArray<string> GetAdaptiveTargets(
        InvocationExpressionSyntax invocation)
    {
        return _adaptiveUpdateTargetUses.TryGetValue(
            invocation,
            out var targets)
                ? targets.ToImmutableArray()
                : ImmutableArray<string>.Empty;
    }

    public ImmutableArray<SyntaxNode> GetAdaptiveTargetDesignators(
        InvocationExpressionSyntax invocation)
    {
        return _adaptiveUpdateTargetDesignators.TryGetValue(
            invocation,
            out var designators)
                ? designators
                    .OrderBy(static designator =>
                        designator.SyntaxTree.FilePath,
                        StringComparer.Ordinal)
                    .ThenBy(static designator => designator.SpanStart)
                    .ToImmutableArray()
                : ImmutableArray<SyntaxNode>.Empty;
    }

    public bool TryRegisterAdaptiveUpdate(
        InvocationExpressionSyntax invocation,
        string currentDestinationExpression,
        SyntaxNode? targetDesignator)
    {
        if (!_adaptiveUpdateTargetUses.TryGetValue(
                invocation,
                out var targets))
        {
            targets = [];
            _adaptiveUpdateTargetUses.Add(invocation, targets);
        }

        if (!targets.Contains(
                currentDestinationExpression,
                StringComparer.Ordinal))
        {
            targets.Add(currentDestinationExpression);
        }

        if (targetDesignator is not null)
        {
            if (!_adaptiveUpdateTargetDesignators.TryGetValue(
                    invocation,
                    out var designators))
            {
                designators = [];
                _adaptiveUpdateTargetDesignators.Add(
                    invocation,
                    designators);
            }

            if (!designators.Any(existing =>
                    ReferenceEquals(
                        existing.SyntaxTree,
                        targetDesignator.SyntaxTree) &&
                    existing.Span == targetDesignator.Span))
            {
                designators.Add(targetDesignator);
            }
        }

        if (_adaptiveUpdateTargets.TryGetValue(
                invocation,
                out var existingTarget))
        {
            return StringComparer.Ordinal.Equals(
                existingTarget,
                currentDestinationExpression);
        }

        _adaptiveUpdateTargets.Add(
            invocation,
            currentDestinationExpression);
        return true;
    }

    public string AllocateGuardName(
        string preferredName,
        INamedTypeSymbol mapperType)
    {
        if (_usedGuardNames.Count == 0)
        {
            _usedGuardNames.UnionWith(
                UserResultMappingPlanner.BuildUsedLocalNames(mapperType));
        }

        return UserResultMappingPlanner.AllocateName(
            preferredName,
            _usedGuardNames);
    }
}

internal readonly record struct ReadOnlyMemberUpdateTarget(
    string MemberName,
    ITypeSymbol MemberType,
    string Expression,
    ISymbol Member);
