using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DeclarativeIntrinsic
{
    public static bool TryGetKind(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out DeclarativeIntrinsicKind kind,
        out IMethodSymbol method)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(
            invocation,
            cancellationToken);
        method = symbolInfo.Symbol as IMethodSymbol ??
            symbolInfo.CandidateSymbols
                .OfType<IMethodSymbol>()
                .FirstOrDefault(IsTypeMapperIntrinsic) ??
            null!;

        if (method is null || !IsTypeMapperIntrinsic(method))
        {
            kind = default;
            return false;
        }

        method = method.OriginalDefinition;
        kind = method.Name switch
        {
            "Auto" => DeclarativeIntrinsicKind.Auto,
            "Ignore" => DeclarativeIntrinsicKind.Ignore,
            "Map" => DeclarativeIntrinsicKind.Map,
            "Create" => DeclarativeIntrinsicKind.Create,
            "Update" => DeclarativeIntrinsicKind.Update,
            "Value" => DeclarativeIntrinsicKind.Value,
            "ByConvention" => DeclarativeIntrinsicKind.ByConvention,
            _ => default
        };

        return method.Name is
            "Auto" or
            "Ignore" or
            "Map" or
            "Create" or
            "Update" or
            "Value" or
            "ByConvention";
    }

    public static bool Contains(
        CSharpSyntaxNode syntax,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var name in syntax.DescendantNodesAndSelf()
                     .OfType<SimpleNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryGetKind(
                    name,
                    semanticModel,
                    cancellationToken,
                    out _))
            {
                return true;
            }
        }

        foreach (var expression in syntax.DescendantNodesAndSelf()
                     .OfType<ExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var typeInfo = semanticModel.GetTypeInfo(
                expression,
                cancellationToken);

            if (ContainsCompileTimeDslType(typeInfo.Type) ||
                ContainsCompileTimeDslType(typeInfo.ConvertedType))
            {
                return true;
            }
        }

        return false;
    }

    public static bool ContainsUnlowered(
        ExpressionSyntax expression,
        IReadOnlyDictionary<
            InvocationExpressionSyntax,
            TypeMapperNestedMapExpressionModel> nestedMapMappings,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var invocation in expression
                     .DescendantNodesAndSelf()
                     .OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetKind(
                    invocation,
                    semanticModel,
                    cancellationToken,
                    out var kind,
                    out _))
            {
                if (semanticModel.GetOperation(
                        invocation,
                        cancellationToken) is { } operation &&
                    ContainsCompileTimeDslType(operation.Type))
                {
                    return true;
                }

                continue;
            }

            var loweredNestedMap =
                (kind is
                    DeclarativeIntrinsicKind.Map or
                    DeclarativeIntrinsicKind.Create or
                    DeclarativeIntrinsicKind.Update) &&
                nestedMapMappings.ContainsKey(invocation);

            if (kind == DeclarativeIntrinsicKind.Value ||
                loweredNestedMap)
            {
                continue;
            }

            return true;
        }

        foreach (var name in expression
                     .DescendantNodesAndSelf()
                     .OfType<SimpleNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if ((name.Parent is InvocationExpressionSyntax invocation &&
                 ReferenceEquals(invocation.Expression, name)) ||
                (name.Parent is MemberAccessExpressionSyntax access &&
                 ReferenceEquals(access.Name, name) &&
                 access.Parent is InvocationExpressionSyntax
                     memberInvocation &&
                 ReferenceEquals(
                     memberInvocation.Expression,
                     access)))
            {
                continue;
            }

            if (TryGetKind(
                    name,
                    semanticModel,
                    cancellationToken,
                    out _))
            {
                return true;
            }
        }

        var unwrapped = UnwrapTransparentSyntax(expression);
        var expressionType = semanticModel.GetTypeInfo(
                unwrapped,
                cancellationToken)
            .Type;

        if (!ContainsCompileTimeDslType(expressionType))
        {
            return false;
        }

        if (unwrapped is InvocationExpressionSyntax rootInvocation &&
            TryGetKind(
                rootInvocation,
                semanticModel,
                cancellationToken,
                out var rootKind,
                out _) &&
            (rootKind == DeclarativeIntrinsicKind.Value ||
             nestedMapMappings.ContainsKey(rootInvocation)))
        {
            return false;
        }

        if (unwrapped is IdentifierNameSyntax identifier &&
            semanticModel.GetSymbolInfo(
                    identifier,
                    cancellationToken)
                .Symbol is ILocalSymbol &&
            TryGetValueType(expressionType, out _))
        {
            return false;
        }

        if (unwrapped is ConditionalExpressionSyntax or
            SwitchExpressionSyntax ||
            TryGetWrapperCast(
                unwrapped,
                MetadataNames.Member,
                semanticModel,
                cancellationToken,
                out _,
                out _) ||
            TryGetWrapperCast(
                unwrapped,
                MetadataNames.ConstructorParameter,
                semanticModel,
                cancellationToken,
                out _,
                out _))
        {
            return false;
        }

        return true;
    }

    public static bool TryGetKind(
        SimpleNameSyntax name,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out DeclarativeIntrinsicKind kind)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(
            name,
            cancellationToken);
        var method = symbolInfo.Symbol as IMethodSymbol ??
            symbolInfo.CandidateSymbols
                .OfType<IMethodSymbol>()
                .FirstOrDefault(IsTypeMapperIntrinsic);

        if (method is null || !IsTypeMapperIntrinsic(method))
        {
            kind = default;
            return false;
        }

        kind = method.Name switch
        {
            "Auto" => DeclarativeIntrinsicKind.Auto,
            "Ignore" => DeclarativeIntrinsicKind.Ignore,
            "Map" => DeclarativeIntrinsicKind.Map,
            "Create" => DeclarativeIntrinsicKind.Create,
            "Update" => DeclarativeIntrinsicKind.Update,
            "Value" => DeclarativeIntrinsicKind.Value,
            "ByConvention" => DeclarativeIntrinsicKind.ByConvention,
            _ => default
        };

        return method.Name is
            "Auto" or
            "Ignore" or
            "Map" or
            "Create" or
            "Update" or
            "Value" or
            "ByConvention";
    }

    public static bool TryGetValueType(
        ITypeSymbol? type,
        out ITypeSymbol valueType)
    {
        if (type is INamedTypeSymbol
            {
                TypeArguments.Length: 1
            } namedType &&
            StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    namedType.OriginalDefinition),
                MetadataNames.GenericValueMarker))
        {
            valueType = namedType.TypeArguments[0]
                .WithNullableAnnotation(
                    namedType.TypeArgumentNullableAnnotations[0]);
            return true;
        }

        valueType = null!;
        return false;
    }

    public static bool TryGetWrapperTargetType(
        ExpressionSyntax expression,
        string wrapperMetadataName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out ITypeSymbol targetType)
    {
        for (ExpressionSyntax? current = expression;
             current is not null;
             current = current.Parent as ExpressionSyntax)
        {
            var typeInfo = semanticModel.GetTypeInfo(
                current,
                cancellationToken);

            if (TryGetWrapperValueType(
                    typeInfo.ConvertedType,
                    wrapperMetadataName,
                    out targetType) ||
                TryGetWrapperValueType(
                    typeInfo.Type,
                    wrapperMetadataName,
                    out targetType))
            {
                return true;
            }
        }

        targetType = null!;
        return false;
    }

    public static bool TryGetWrapperCast(
        ExpressionSyntax expression,
        string wrapperMetadataName,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out CastExpressionSyntax wrapperCast,
        out ITypeSymbol targetType)
    {
        expression = UnwrapTransparentSyntax(expression);

        if (expression is CastExpressionSyntax cast &&
            semanticModel.GetTypeInfo(
                    cast.Type,
                    cancellationToken)
                .Type is { } castType &&
            TryGetWrapperValueType(
                castType,
                wrapperMetadataName,
                out targetType))
        {
            wrapperCast = cast;
            return true;
        }

        wrapperCast = null!;
        targetType = null!;
        return false;
    }

    public static ExpressionSyntax UnwrapTransparentSyntax(
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

    public static bool HasExactTargetType(
        ITypeSymbol assertedType,
        ITypeSymbol targetType,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType)
    {
        var semanticMapperType = semanticModel.Compilation
                .GetTypeByMetadataName(
                    SymbolNameHelper.GetFullMetadataName(mapperType)) ??
            mapperType;
        var substitutions = MapperTypeSubstitution.BuildForHierarchy(
            semanticMapperType);
        assertedType = MapperTypeSubstitution.Substitute(
            assertedType,
            substitutions,
            semanticModel.Compilation);
        targetType = MapperTypeSubstitution.Substitute(
            targetType,
            substitutions,
            semanticModel.Compilation);

        return SymbolEqualityComparer.IncludeNullability.Equals(
            assertedType,
            targetType);
    }

    public static bool ValidateValueTargets(
        ExpressionSyntax expression,
        ITypeSymbol? targetType,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        CancellationToken cancellationToken)
    {
        if (HasUnsupportedValueMarkerProducer(
                expression,
                semanticModel,
                cancellationToken))
        {
            return false;
        }

        if (targetType is null)
        {
            var typeInfo = semanticModel.GetTypeInfo(
                expression,
                cancellationToken);

            if (!TryGetValueType(typeInfo.Type, out targetType) &&
                !TryGetValueType(typeInfo.ConvertedType, out targetType))
            {
                targetType = null;
            }
        }

        if (targetType is not null)
        {
            var typeInfo = semanticModel.GetTypeInfo(
                expression,
                cancellationToken);

            if ((TryGetValueType(
                     typeInfo.Type,
                     out var expressionValueType) ||
                 TryGetValueType(
                     typeInfo.ConvertedType,
                     out expressionValueType)) &&
                !HasExactTargetType(
                    expressionValueType,
                    targetType,
                    semanticModel,
                    mapperType))
            {
                return false;
            }

            foreach (var identifier in expression
                         .DescendantNodesAndSelf()
                         .OfType<IdentifierNameSyntax>())
            {
                var symbol = semanticModel.GetSymbolInfo(
                        identifier,
                        cancellationToken)
                    .Symbol;

                if (symbol is not (ILocalSymbol or IParameterSymbol) ||
                    !TryGetValueType(
                        symbol switch
                        {
                            ILocalSymbol local => local.Type,
                            IParameterSymbol parameter => parameter.Type,
                            _ => null
                        },
                        out var referencedValueType))
                {
                    continue;
                }

                if (!HasExactTargetType(
                        referencedValueType,
                        targetType,
                        semanticModel,
                        mapperType))
                {
                    return false;
                }
            }
        }

        foreach (var invocation in expression
                     .DescendantNodesAndSelf()
                     .OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetKind(
                    invocation,
                    semanticModel,
                    cancellationToken,
                    out var kind,
                    out _) ||
                kind != DeclarativeIntrinsicKind.Value)
            {
                continue;
            }

            if (semanticModel.GetOperation(
                    invocation,
                    cancellationToken) is not IInvocationOperation
                {
                    TargetMethod:
                    {
                        IsGenericMethod: true,
                        TypeArguments.Length: 1
                    } valueMethod
                } ||
                targetType is null)
            {
                return false;
            }

            if (!HasSupportedTerminalPlacement(
                    expression,
                    invocation,
                    semanticModel,
                    cancellationToken))
            {
                return false;
            }

            var assertedType = valueMethod.TypeArguments[0]
                .WithNullableAnnotation(
                    valueMethod.TypeArgumentNullableAnnotations[0]);

            if (!HasExactTargetType(
                    assertedType,
                    targetType,
                    semanticModel,
                    mapperType))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool HasSupportedTerminalPlacement(
        ExpressionSyntax root,
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        SyntaxNode current = expression;

        while (!ReferenceEquals(current, root))
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (current.Parent)
            {
                case ParenthesizedExpressionSyntax parenthesized
                    when ReferenceEquals(
                        parenthesized.Expression,
                        current):
                    current = parenthesized;
                    continue;

                case PostfixUnaryExpressionSyntax postfix
                    when postfix.IsKind(
                             SyntaxKind.SuppressNullableWarningExpression) &&
                         ReferenceEquals(postfix.Operand, current):
                    current = postfix;
                    continue;

                case ConditionalExpressionSyntax conditional
                    when ReferenceEquals(conditional.WhenTrue, current) ||
                         ReferenceEquals(conditional.WhenFalse, current):
                    current = conditional;
                    continue;

                case SwitchExpressionArmSyntax arm
                    when ReferenceEquals(arm.Expression, current):
                    current = arm;
                    continue;

                case SwitchExpressionSyntax switchExpression
                    when current is SwitchExpressionArmSyntax &&
                         switchExpression.Arms.Any(arm =>
                             ReferenceEquals(arm, current)):
                    current = switchExpression;
                    continue;

                case CastExpressionSyntax cast
                    when ReferenceEquals(cast.Expression, current) &&
                         (TryGetWrapperCast(
                              cast,
                              MetadataNames.Member,
                              semanticModel,
                              cancellationToken,
                              out _,
                              out _) ||
                          TryGetWrapperCast(
                              cast,
                              MetadataNames.ConstructorParameter,
                              semanticModel,
                              cancellationToken,
                              out _,
                              out _)):
                    current = cast;
                    continue;

                default:
                    return false;
            }
        }

        return true;
    }

    public static ITypeSymbol? GetEffectiveValueType(
        IOperation operation)
    {
        if (operation is IInvocationOperation
            {
                TargetMethod:
                {
                    IsGenericMethod: true,
                    TypeArguments.Length: 1
                } method
            } &&
            IsTypeMapperIntrinsic(method) &&
            method.Name == "Value")
        {
            return method.TypeArguments[0]
                .WithNullableAnnotation(
                    method.TypeArgumentNullableAnnotations[0]);
        }

        return TryGetValueType(operation.Type, out var valueType)
            ? valueType
            : null;
    }

    private static bool IsTypeMapperIntrinsic(IMethodSymbol method)
    {
        method = method.OriginalDefinition;
        var typeMapper = method.ContainingAssembly
            .GetTypeByMetadataName(MetadataNames.TypeMapper);

        return typeMapper is not null &&
               SymbolEqualityComparer.Default.Equals(
                   method.ContainingType,
                   typeMapper);
    }

    private static bool HasUnsupportedValueMarkerProducer(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var candidate in expression
                     .DescendantNodesAndSelf()
                     .OfType<ExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryGetValueType(
                    semanticModel.GetTypeInfo(
                            candidate,
                            cancellationToken)
                        .Type,
                    out _))
            {
                continue;
            }

            var unwrapped = UnwrapTransparentSyntax(candidate);

            if (unwrapped is InvocationExpressionSyntax invocation &&
                TryGetKind(
                    invocation,
                    semanticModel,
                    cancellationToken,
                    out var kind,
                    out _) &&
                kind == DeclarativeIntrinsicKind.Value)
            {
                continue;
            }

            if (unwrapped is IdentifierNameSyntax identifier &&
                semanticModel.GetSymbolInfo(
                        identifier,
                        cancellationToken)
                    .Symbol is ILocalSymbol)
            {
                continue;
            }

            if (unwrapped is ConditionalExpressionSyntax or
                SwitchExpressionSyntax)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool ContainsCompileTimeDslType(
        ITypeSymbol? type)
    {
        return ContainsCompileTimeDslType(
            type,
            new HashSet<ITypeSymbol>(
                SymbolEqualityComparer.Default));
    }

    private static bool ContainsCompileTimeDslType(
        ITypeSymbol? type,
        HashSet<ITypeSymbol> visited)
    {
        if (type is null || !visited.Add(type))
        {
            return false;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return ContainsCompileTimeDslType(
                arrayType.ElementType,
                visited);
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var metadataName = SymbolNameHelper.GetFullMetadataName(
            namedType.OriginalDefinition);

        if (metadataName is
                MetadataNames.Member or
                MetadataNames.ConstructorParameter or
                MetadataNames.GenericValueMarker or
                MetadataNames.MappingContextMarker)
        {
            return true;
        }

        for (var current = namedType;
             current is not null;
             current = current.BaseType)
        {
            metadataName = SymbolNameHelper.GetFullMetadataName(
                current.OriginalDefinition);

            if (metadataName is
                MetadataNames.MemberMarker or
                MetadataNames.ConstructorMarker)
            {
                return true;
            }
        }

        if (namedType.TypeArguments.Any(typeArgument =>
                ContainsCompileTimeDslType(
                    typeArgument,
                    visited)))
        {
            return true;
        }

        return namedType.DelegateInvokeMethod is { } invokeMethod &&
               (ContainsCompileTimeDslType(
                    invokeMethod.ReturnType,
                    visited) ||
                invokeMethod.Parameters.Any(parameter =>
                    ContainsCompileTimeDslType(
                        parameter.Type,
                        visited)));
    }

    private static bool TryGetWrapperValueType(
        ITypeSymbol? type,
        string wrapperMetadataName,
        out ITypeSymbol valueType)
    {
        if (type is INamedTypeSymbol
            {
                TypeArguments.Length: 1
            } namedType &&
            StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    namedType.OriginalDefinition),
                wrapperMetadataName))
        {
            valueType = namedType.TypeArguments[0]
                .WithNullableAnnotation(
                    namedType.TypeArgumentNullableAnnotations[0]);
            return true;
        }

        valueType = null!;
        return false;
    }
}

internal enum DeclarativeIntrinsicKind
{
    Auto,
    Ignore,
    Map,
    Create,
    Update,
    Value,
    ByConvention
}
