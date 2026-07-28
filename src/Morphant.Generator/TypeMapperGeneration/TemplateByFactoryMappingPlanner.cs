using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TemplateByFactoryMappingPlanner
{
    private const string ByFactoryMarkerMetadataName =
        "Morphant.Markers.IByFactoryMarker`1";

    private const string FuncMetadataName =
        "System.Func`1";

    private const string TypeMapperMetadataName =
        "Morphant.TypeMapper";

    private const string UnsupportedBlockMessage =
        "ByFactory block lambdas currently support only local " +
        "variable declarations followed by a single return " +
        "statement.";

    private const string UnsupportedCaptureMessage =
        "ByFactory contains a capture that cannot be transferred " +
        "to the generated mapper.";

    public static bool TryBuild(
        ImmutableArray<TemplateObjectArgumentSyntax> arguments,
        SemanticModel semanticModel,
        HashSet<ISymbol> allowedCapturedSymbols,
        CancellationToken cancellationToken,
        out TemplateFactorySyntaxPlan? factory)
    {
        factory = null;

        if (!ContainsMarker(
                arguments,
                semanticModel,
                cancellationToken))
        {
            return false;
        }

        if (arguments.Length != 1)
        {
            return true;
        }

        var markerArgument = arguments[0];

        if (markerArgument.Syntax.NameColon is
                { Name.Identifier.ValueText: not "marker" } ||
            !IsMarker(
                markerArgument.Value,
                semanticModel,
                cancellationToken) ||
            UnwrapParentheses(markerArgument.Value) is not
                InvocationExpressionSyntax markerInvocation ||
            !IsByFactoryInvocation(
                markerInvocation,
                semanticModel,
                cancellationToken) ||
            markerInvocation.ArgumentList.Arguments.Count != 1)
        {
            return true;
        }

        var factoryArgument =
            markerInvocation.ArgumentList.Arguments[0];

        if (factoryArgument.NameColon is
                { Name.Identifier.ValueText: not "factory" })
        {
            return true;
        }

        var factoryExpression =
            UnwrapParentheses(factoryArgument.Expression);

        if (factoryExpression is
            ParenthesizedLambdaExpressionSyntax
            {
                ParameterList.Parameters.Count: 0
            } lambda)
        {
            factory = BuildLambda(
                lambda,
                semanticModel,
                allowedCapturedSymbols,
                cancellationToken);
            return true;
        }

        if (semanticModel.GetTypeInfo(
                    factoryArgument.Expression,
                    cancellationToken)
                .ConvertedType is not
                INamedTypeSymbol convertedType ||
            !StringComparer.Ordinal.Equals(
                SymbolNameHelper.GetFullMetadataName(
                    convertedType.OriginalDefinition),
                FuncMetadataName))
        {
            return true;
        }

        if (ContainsUnsupportedCapture(
                [factoryArgument.Expression],
                factoryArgument.Expression,
                allowedCapturedSymbols,
                semanticModel,
                cancellationToken))
        {
            factory = TemplateFactorySyntaxPlan.Unsupported(
                UnsupportedCaptureMessage);
            return true;
        }

        var reservedNames = BuildReservedNames(
            factoryArgument.Expression);
        var placeholderOrdinal = 0;
        var placeholder = AllocatePlaceholder(
            ref placeholderOrdinal,
            reservedNames);
        var local = new TemplateFactoryRuntimeLocalSyntax(
            placeholder,
            PreferredName: "factory",
            TypeMapperMappingTypePolicy.GetGeneratedTypeName(
                convertedType),
            factoryArgument.Expression);

        factory = new TemplateFactorySyntaxPlan(
            [local],
            new Dictionary<ISymbol, string>(
                SymbolEqualityComparer.Default),
            ResultExpression: null,
            InvokedLocalPlaceholder: placeholder,
            UnsupportedMessage: null);
        return true;
    }

    private static TemplateFactorySyntaxPlan BuildLambda(
        ParenthesizedLambdaExpressionSyntax lambda,
        SemanticModel semanticModel,
        HashSet<ISymbol> allowedCapturedSymbols,
        CancellationToken cancellationToken)
    {
        if (!TransferableLambdaSyntax.TryGetResult(
                lambda,
                out var declarations,
                out var resultExpression))
        {
            return TemplateFactorySyntaxPlan.Unsupported(
                UnsupportedBlockMessage);
        }

        var runtimeLocals =
            ImmutableArray.CreateBuilder<
                TemplateFactoryRuntimeLocalSyntax>();
        var localPlaceholders =
            new Dictionary<ISymbol, string>(
                SymbolEqualityComparer.Default);
        var allowedSymbols =
            new HashSet<ISymbol>(
                allowedCapturedSymbols,
                SymbolEqualityComparer.Default);
        var reservedNames = BuildReservedNames(lambda);
        var ordinal = 0;

        foreach (var declaration in declarations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!declaration.UsingKeyword.IsKind(
                    SyntaxKind.None) ||
                declaration.Declaration.Variables.Count == 0)
            {
                return TemplateFactorySyntaxPlan.Unsupported(
                    UnsupportedBlockMessage);
            }

            foreach (var variable in
                     declaration.Declaration.Variables)
            {
                if (variable.Initializer?.Value is not
                        { } initializer ||
                    semanticModel.GetDeclaredSymbol(
                        variable,
                        cancellationToken) is not
                        ILocalSymbol local)
                {
                    return TemplateFactorySyntaxPlan.Unsupported(
                        UnsupportedBlockMessage);
                }

                var placeholder = AllocatePlaceholder(
                    ref ordinal,
                    reservedNames);
                var declarationType =
                    declaration.Declaration.Type.IsVar
                        ? "var"
                        : TypeMapperMappingTypePolicy
                            .GetGeneratedTypeName(
                                local.Type.WithNullableAnnotation(
                                    local.NullableAnnotation));

                localPlaceholders.Add(
                    local,
                    placeholder);
                allowedSymbols.Add(local);
                runtimeLocals.Add(
                    new TemplateFactoryRuntimeLocalSyntax(
                        placeholder,
                        local.Name,
                        declarationType,
                        initializer));
            }
        }

        if (ContainsUnsupportedCapture(
                runtimeLocals
                    .Select(static local =>
                        local.Initializer)
                    .Append(resultExpression),
                lambda,
                allowedSymbols,
                semanticModel,
                cancellationToken))
        {
            return TemplateFactorySyntaxPlan.Unsupported(
                UnsupportedCaptureMessage);
        }

        return new TemplateFactorySyntaxPlan(
            runtimeLocals.ToImmutable(),
            localPlaceholders,
            resultExpression,
            InvokedLocalPlaceholder: null,
            UnsupportedMessage: null);
    }

    private static bool ContainsUnsupportedCapture(
        IEnumerable<ExpressionSyntax> expressions,
        SyntaxNode transferredSyntax,
        HashSet<ISymbol> allowedSymbols,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var expression in expressions)
        {
            foreach (var identifier in expression
                         .DescendantNodesAndSelf()
                         .OfType<SimpleNameSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var symbol = semanticModel.GetSymbolInfo(
                        identifier,
                        cancellationToken)
                    .Symbol;

                if (symbol is ILocalSymbol
                    {
                        IsConst: true
                    })
                {
                    continue;
                }

                if (symbol is ILocalSymbol or
                    IParameterSymbol)
                {
                    if (!allowedSymbols.Contains(symbol) &&
                        !IsDeclaredWithin(
                            symbol,
                            transferredSyntax))
                    {
                        return true;
                    }

                    continue;
                }

                if (symbol is IRangeVariableSymbol ||
                    symbol is IMethodSymbol
                    {
                        MethodKind:
                            MethodKind.LocalFunction,
                        IsStatic: false
                    })
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsDeclaredWithin(
        ISymbol symbol,
        SyntaxNode syntax)
    {
        foreach (var reference in
                 symbol.DeclaringSyntaxReferences)
        {
            if (ReferenceEquals(
                    reference.SyntaxTree,
                    syntax.SyntaxTree) &&
                syntax.FullSpan.Contains(
                    reference.Span))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> BuildReservedNames(
        SyntaxNode syntax)
    {
        return new HashSet<string>(
            syntax.DescendantTokens()
                .Where(static token =>
                    token.IsKind(
                        SyntaxKind.IdentifierToken))
                .Select(static token =>
                    token.ValueText),
            StringComparer.Ordinal);
    }

    private static string AllocatePlaceholder(
        ref int ordinal,
        HashSet<string> reservedNames)
    {
        while (true)
        {
            var candidate =
                "__morphantFactoryLocal" +
                ordinal++.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);

            if (reservedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static bool ContainsMarker(
        ImmutableArray<TemplateObjectArgumentSyntax> arguments,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var argument in arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsMarker(
                    argument.Value,
                    semanticModel,
                    cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMarker(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        expression = UnwrapParentheses(expression);

        var typeInfo = semanticModel.GetTypeInfo(
            expression,
            cancellationToken);

        return IsMarkerType(typeInfo.Type) ||
               IsMarkerType(typeInfo.ConvertedType) ||
               semanticModel.GetSymbolInfo(
                       expression,
                       cancellationToken)
                   .Symbol is IMethodSymbol method &&
               IsMarkerType(method.ReturnType);
    }

    private static bool IsMarkerType(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol namedType &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       namedType.OriginalDefinition),
                   ByFactoryMarkerMetadataName);
    }

    private static bool IsByFactoryInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return semanticModel.GetSymbolInfo(
                    invocation,
                    cancellationToken)
                .Symbol is IMethodSymbol
                {
                    Name: "ByFactory",
                    ContainingType: { } containingType
                } &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       containingType),
                   TypeMapperMetadataName);
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
}

internal sealed record TemplateFactorySyntaxPlan(
    ImmutableArray<TemplateFactoryRuntimeLocalSyntax> RuntimeLocals,
    IReadOnlyDictionary<ISymbol, string> RuntimeLocalPlaceholders,
    ExpressionSyntax? ResultExpression,
    string? InvokedLocalPlaceholder,
    string? UnsupportedMessage)
{
    public static TemplateFactorySyntaxPlan Unsupported(
        string message)
    {
        return new TemplateFactorySyntaxPlan(
            [],
            new Dictionary<ISymbol, string>(
                SymbolEqualityComparer.Default),
            ResultExpression: null,
            InvokedLocalPlaceholder: null,
            UnsupportedMessage: message);
    }
}

internal readonly record struct TemplateFactoryRuntimeLocalSyntax(
    string PlaceholderName,
    string PreferredName,
    string DeclarationType,
    ExpressionSyntax Initializer);
