using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Morphant.Generator.MappingPair;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DeclarativeNestedMapExpression
{
    private const string TypeMapperMetadataName =
        "Morphant.TypeMapper";

    private const string MapMarkerMetadataName =
        "Morphant.Markers.MapMarker";

    private const string GenericMapMarkerMetadataName =
        "Morphant.Markers.MapMarker`1";

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
                        semanticModel.Compilation)
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

            if (invocation.Ancestors()
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

    private static bool TryBuildMapping(
        InvocationExpressionSyntax invocation,
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
            sourceType = sourceTypeInfo.Type?.WithNullableAnnotation(
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
            if (targetContext is not
                    {
                        CurrentDestinationExpression: { } currentDestination
                    } adaptiveTarget ||
                !HasRuntimeDestinationConversion(
                    adaptiveTarget.Type,
                    destinationType,
                    semanticModel.Compilation,
                    mapperType,
                    cancellationToken) ||
                !usageRegistry.TryRegisterAdaptiveUpdate(
                    invocation,
                    currentDestination))
            {
                mapping = default;
                return false;
            }

            generatedDestinationExpression = currentDestination;
            generatedDestinationType = adaptiveTarget.Type;
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
            incompatibleDestinationName);
        return true;
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
                "Morphant.Members.Member`1"))
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
            resultName + "." + Identifier(property.Name));
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
        method = semanticModel.GetSymbolInfo(
                invocation,
                cancellationToken)
            .Symbol as IMethodSymbol ?? null!;

        if (method is null ||
            method.Name is not ("Map" or "Create" or "Update") ||
            !method.IsStatic ||
            !StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    method.ContainingType),
                TypeMapperMetadataName) ||
            method.ReturnType is not INamedTypeSymbol returnType)
        {
            return false;
        }

        var returnMetadataName =
            SymbolNameHelper.GetFullMetadataName(
                returnType.OriginalDefinition);

        return returnMetadataName is
            MapMarkerMetadataName or
            GenericMapMarkerMetadataName;
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
    string DestinationTypeName,
    DeclarativeNestedMapOperation Operation,
    string? InferredSourceMemberName,
    string? GeneratedDestinationExpression,
    ITypeSymbol? GeneratedDestinationType,
    bool GuardNullDestination,
    string? GuardVariableName,
    string RuntimeDestinationTypeName,
    string? CompatibleDestinationName,
    string? IncompatibleDestinationName
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
    string? CurrentDestinationExpression);

internal sealed class DeclarativeNestedMapUsageRegistry
{
    private readonly Dictionary<InvocationExpressionSyntax, string>
        _adaptiveUpdateTargets = new(
            DeclarativeNestedMapExpression
                .InvocationReferenceComparer.Instance);
    private readonly HashSet<string> _usedGuardNames =
        new(StringComparer.Ordinal);

    public bool TryRegisterAdaptiveUpdate(
        InvocationExpressionSyntax invocation,
        string currentDestinationExpression)
    {
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
    string Expression);
