using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

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
                    targetType,
                    semanticModel,
                    mapperType,
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
            if (targetType is not null &&
                !HasWarningFreeImplicitConversion(
                    markerDestination,
                    targetType,
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
                 targetType is null)
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

    private static bool TryBuildMapping(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        ITypeSymbol? targetType,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken,
        out TypeMapperNestedMapExpressionModel mapping)
    {
        var operation = semanticModel.GetOperation(
            invocation,
            cancellationToken) as IInvocationOperation;

        if (operation is null ||
            method.Parameters.Length is not (1 or 2) ||
            operation.Arguments.FirstOrDefault(argument =>
                argument.Parameter?.Name == "source") is not
                { Syntax: ArgumentSyntax sourceArgument })
        {
            mapping = default;
            return false;
        }

        var sourceTypeInfo = semanticModel.GetTypeInfo(
            sourceArgument.Expression,
            cancellationToken);
        var sourceType = sourceTypeInfo.Type;

        if (sourceType is null ||
            !CanUseAsGenericArgument(sourceType))
        {
            mapping = default;
            return false;
        }

        sourceType = sourceType.WithNullableAnnotation(
            sourceTypeInfo.Nullability.Annotation);

        ITypeSymbol? destinationType;

        if (method.IsGenericMethod && method.TypeArguments.Length == 1)
        {
            destinationType = method.TypeArguments[0]
                .WithNullableAnnotation(
                    method.TypeArgumentNullableAnnotations[0]);
        }
        else
        {
            destinationType = targetType;
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
            method.Parameters.Length == 2 &&
            !HasCompatibleDestinationArgument(
                operation,
                destinationType,
                semanticModel,
                mapperType,
                cancellationToken))
        {
            mapping = default;
            return false;
        }

        mapping = new TypeMapperNestedMapExpressionModel(
            sourceType,
            destinationType,
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(sourceType),
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                destinationType),
            IsUpdate: method.Parameters.Length == 2);
        return true;
    }

    private static bool HasCompatibleDestinationArgument(
        IInvocationOperation operation,
        ITypeSymbol destinationType,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
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
            method.Name != "Map" ||
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

    private sealed class InvocationReferenceComparer
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
    bool IsUpdate
);
