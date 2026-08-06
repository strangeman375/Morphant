using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DeclarativeDependencyExpressionBuilder
{
    private const string AnnotationKind =
        "MorphantDeclarativeDependency";

    private static readonly SymbolDisplayFormat DependencySymbolFormat = new(
        globalNamespaceStyle:
            SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle:
            SymbolDisplayTypeQualificationStyle
                .NameAndContainingTypesAndNamespaces,
        genericsOptions:
            SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions:
            SymbolDisplayMemberOptions.IncludeContainingType |
            SymbolDisplayMemberOptions.IncludeParameters |
            SymbolDisplayMemberOptions.IncludeType |
            SymbolDisplayMemberOptions.IncludeExplicitInterface,
        parameterOptions:
            SymbolDisplayParameterOptions.IncludeType |
            SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions
                .IncludeNullableReferenceTypeModifier);

    public static bool TryRewrite(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        IParameterSymbol? resultParameter,
        string? resultName,
        SyntaxNode transferScope,
        IReadOnlyDictionary<ISymbol, string>? localSubstitutions,
        ITypeSymbol? fallbackType,
        DeclarativeNestedMapTargetContext? nestedMapTarget,
        DeclarativeNestedMapUsageRegistry? nestedMapUsageRegistry,
        CancellationToken cancellationToken,
        out string rewrittenExpression,
        out TypeMapperDependencyExpressionModel? dependencyExpression)
    {
        if (!DeclarativeNestedMapExpression.TryBuild(
                expression,
                fallbackType,
                nestedMapTarget,
                nestedMapUsageRegistry ??
                new DeclarativeNestedMapUsageRegistry(),
                sourceParameter,
                resultName,
                semanticModel,
                mapperType,
                cancellationToken,
                out var nestedMapMappings))
        {
            rewrittenExpression = string.Empty;
            dependencyExpression = null;
            return false;
        }

        var dependencyRoot = UnwrapTransparentSyntax(expression);
        var candidates = BuildCandidates(
            dependencyRoot,
            semanticModel,
            sourceParameter,
            previousParameter,
            resultParameter,
            fallbackType,
            nestedMapMappings,
            cancellationToken);

        if (candidates.IsEmpty)
        {
            dependencyExpression = null;
            if (!ConstructExpressionRewriter.TryRewriteSyntaxWithAnnotations(
                expression,
                semanticModel,
                mapperType,
                sourceParameter,
                sourceName,
                previousParameter,
                previousSubstitution,
                resultParameter,
                resultName,
                transferScope,
                localSubstitutions,
                ImmutableDictionary<SyntaxNode, SyntaxAnnotation>.Empty,
                nestedMapMappings,
                cancellationToken,
                out var directlyRewritten))
            {
                rewrittenExpression = string.Empty;
                return false;
            }

            rewrittenExpression = directlyRewritten
                .WithoutTrivia()
                .NormalizeWhitespace()
                .ToFullString();
            return true;
        }

        var annotations = candidates.ToDictionary(
            static candidate => (SyntaxNode)candidate.Syntax,
            static candidate => candidate.Annotation,
            SyntaxNodeReferenceComparer.Instance);

        if (!ConstructExpressionRewriter.TryRewriteSyntaxWithAnnotations(
                expression,
                semanticModel,
                mapperType,
                sourceParameter,
                sourceName,
                previousParameter,
                previousSubstitution,
                resultParameter,
                resultName,
                transferScope,
                localSubstitutions,
                annotations,
                nestedMapMappings,
                cancellationToken,
                out var rewritten))
        {
            rewrittenExpression = string.Empty;
            dependencyExpression = null;
            return false;
        }

        var normalized = (ExpressionSyntax)rewritten
            .WithoutTrivia()
            .NormalizeWhitespace();
        var rewrittenCandidates = BuildRewrittenCandidates(
            normalized,
            candidates);
        var rootCandidate = candidates[0];
        var root = new RewrittenCandidate(
            rootCandidate,
            normalized);
        var rootNode = BuildNode(
            root,
            rewrittenCandidates.Where(candidate =>
                    !ReferenceEquals(
                        candidate.Candidate.Annotation,
                        rootCandidate.Annotation) &&
                    normalized.Span.Contains(candidate.Syntax.Span))
                .ToImmutableArray());

        dependencyExpression =
            new TypeMapperDependencyExpressionModel(rootNode);
        rewrittenExpression = dependencyExpression.Render();
        return true;
    }

    private static ExpressionSyntax UnwrapTransparentSyntax(
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

    public static string BuildDeclaredValueKey(
        ISymbol symbol,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
        IParameterSymbol? resultParameter)
    {
        return BuildSymbolKey(
            symbol,
            sourceParameter,
            previousParameter,
            resultParameter);
    }

    private static ImmutableArray<DependencyCandidate> BuildCandidates(
        ExpressionSyntax root,
        SemanticModel semanticModel,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
        IParameterSymbol? resultParameter,
        ITypeSymbol? fallbackType,
        IReadOnlyDictionary<
            InvocationExpressionSyntax,
            TypeMapperNestedMapExpressionModel> nestedMapMappings,
        CancellationToken cancellationToken)
    {
        var result =
            ImmutableArray.CreateBuilder<DependencyCandidate>();
        var index = 0;

        foreach (var expression in root
                     .DescendantNodesAndSelf(
                         descendIntoTrivia: false)
                     .OfType<ExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var operation = semanticModel.GetOperation(
                expression,
                cancellationToken);

            if (operation is null)
            {
                continue;
            }

            operation = UnwrapTransparentOperation(operation);
            var type = DeclarativeNestedMapExpression.GetEffectiveType(
                operation,
                ReferenceEquals(expression, root)
                    ? fallbackType
                    : null,
                nestedMapMappings);

            if (type is null || type.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var key = BuildOperationKey(
                operation,
                sourceParameter,
                previousParameter,
                resultParameter,
                nestedMapMappings,
                cancellationToken);

            if (key is null)
            {
                continue;
            }

            var isRoot = ReferenceEquals(expression, root);

            if (result.Any(candidate =>
                    candidate.Key == key &&
                    candidate.Syntax.Span.Contains(expression.Span)))
            {
                continue;
            }

            if (!isRoot &&
                (!IsUnconditionallyEvaluated(expression, root) ||
                 !CanMaterialize(operation)))
            {
                continue;
            }

            result.Add(
                new DependencyCandidate(
                    expression,
                    key,
                    TypeMapperMappingTypePolicy.GetGeneratedTypeName(type),
                    CanMaterialize(operation),
                    new SyntaxAnnotation(
                        AnnotationKind,
                        (index++).ToString(
                            CultureInfo.InvariantCulture))));
        }

        if (result.Count == 0 ||
            !ReferenceEquals(result[0].Syntax, root))
        {
            return [];
        }

        return result.ToImmutable();
    }

    private static ImmutableArray<RewrittenCandidate>
        BuildRewrittenCandidates(
            ExpressionSyntax rewritten,
            ImmutableArray<DependencyCandidate> candidates)
    {
        var result = ImmutableArray.CreateBuilder<RewrittenCandidate>();
        var occupiedSpans = new HashSet<TextSpan>();

        foreach (var candidate in candidates)
        {
            var syntax = rewritten
                .GetAnnotatedNodes(candidate.Annotation)
                .OfType<ExpressionSyntax>()
                .FirstOrDefault();

            if (syntax is null ||
                syntax.Span == rewritten.Span ||
                !rewritten.Span.Contains(syntax.Span) ||
                !occupiedSpans.Add(syntax.Span))
            {
                continue;
            }

            result.Add(new RewrittenCandidate(candidate, syntax));
        }

        return result.ToImmutable();
    }

    private static TypeMapperDependencyExpressionNodeModel BuildNode(
        RewrittenCandidate current,
        ImmutableArray<RewrittenCandidate> descendants)
    {
        var directChildren = descendants
            .Where(candidate =>
                current.Syntax.Span.Contains(candidate.Syntax.Span) &&
                !descendants.Any(parent =>
                    !ReferenceEquals(parent.Syntax, candidate.Syntax) &&
                    current.Syntax.Span.Contains(parent.Syntax.Span) &&
                    parent.Syntax.Span.Contains(candidate.Syntax.Span) &&
                    parent.Syntax.Span != current.Syntax.Span &&
                    parent.Syntax.Span != candidate.Syntax.Span))
            .OrderBy(static candidate => candidate.Syntax.SpanStart)
            .ToImmutableArray();
        var childModels =
            ImmutableArray.CreateBuilder<
                TypeMapperDependencyExpressionChildModel>(
                directChildren.Length);
        var replacements = new Dictionary<ExpressionSyntax, string>(
            SyntaxNodeReferenceComparer.ExpressionInstance);
        var usedNames = new HashSet<string>(
            current.Syntax.DescendantTokens()
                .Where(static token =>
                    token.IsKind(SyntaxKind.IdentifierToken))
                .Select(static token => token.ValueText),
            StringComparer.Ordinal);

        for (var index = 0;
             index < directChildren.Length;
             index++)
        {
            var child = directChildren[index];
            var placeholder = AllocatePlaceholder(
                current.Candidate.Annotation.Data!,
                index,
                usedNames);
            var nested = descendants.Where(candidate =>
                    child.Syntax.Span.Contains(candidate.Syntax.Span) &&
                    candidate.Syntax.Span != child.Syntax.Span)
                .ToImmutableArray();

            replacements.Add(child.Syntax, placeholder);
            childModels.Add(
                new TypeMapperDependencyExpressionChildModel(
                    placeholder,
                    BuildNode(child, nested)));
        }

        var template = current.Syntax
            .ReplaceNodes(
                replacements.Keys,
                (original, _) =>
                    SyntaxFactory.IdentifierName(
                            replacements[original])
                        .WithTriviaFrom(original))
            .WithoutTrivia()
            .NormalizeWhitespace()
            .ToFullString();

        return new TypeMapperDependencyExpressionNodeModel(
            current.Candidate.Key,
            current.Candidate.ValueTypeName,
            current.Candidate.CanMaterialize,
            template,
            childModels.ToImmutable());
    }

    private static string AllocatePlaceholder(
        string annotationId,
        int childIndex,
        HashSet<string> usedNames)
    {
        var stem =
            "__morphantDependencyPart" +
            annotationId +
            "_" +
            childIndex.ToString(CultureInfo.InvariantCulture) +
            "__";
        var candidate = stem;

        for (var suffix = 1; !usedNames.Add(candidate); suffix++)
        {
            candidate = stem +
                suffix.ToString(CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    private static IOperation UnwrapTransparentOperation(
        IOperation operation)
    {
        while (true)
        {
            switch (operation)
            {
                case IConversionOperation
                {
                    IsImplicit: true,
                    Conversion.IsUserDefined: false,
                    Operand: var operand
                }:
                    operation = operand;
                    continue;

                case IParenthesizedOperation
                {
                    Operand: var operand
                }:
                    operation = operand;
                    continue;

                default:
                    return operation;
            }
        }
    }

    private static bool CanMaterialize(IOperation operation)
    {
        operation = UnwrapTransparentOperation(operation);

        if (operation.ConstantValue.HasValue)
        {
            return false;
        }

        return operation is not (
            IParameterReferenceOperation or
            ILocalReferenceOperation or
            IInstanceReferenceOperation or
            ITypeOfOperation or
            INameOfOperation or
            IDefaultValueOperation);
    }

    private static bool IsUnconditionallyEvaluated(
        ExpressionSyntax expression,
        ExpressionSyntax root)
    {
        for (SyntaxNode? current = expression;
             current is not null &&
             !ReferenceEquals(current, root);
             current = current.Parent)
        {
            var parent = current.Parent;

            if (parent is null)
            {
                return false;
            }

            if (parent is LambdaExpressionSyntax or
                AnonymousMethodExpressionSyntax)
            {
                return false;
            }

            if (parent is ConditionalExpressionSyntax conditional &&
                !ReferenceEquals(current, conditional.Condition))
            {
                return false;
            }

            if (parent is BinaryExpressionSyntax binary &&
                ReferenceEquals(current, binary.Right) &&
                binary.IsKind(SyntaxKind.LogicalAndExpression) ||
                parent is BinaryExpressionSyntax orBinary &&
                ReferenceEquals(current, orBinary.Right) &&
                orBinary.IsKind(SyntaxKind.LogicalOrExpression) ||
                parent is BinaryExpressionSyntax coalesce &&
                ReferenceEquals(current, coalesce.Right) &&
                coalesce.IsKind(SyntaxKind.CoalesceExpression))
            {
                return false;
            }

            if (parent is ConditionalAccessExpressionSyntax conditionalAccess &&
                ReferenceEquals(current, conditionalAccess.WhenNotNull) ||
                parent is SwitchExpressionArmSyntax ||
                parent is QueryExpressionSyntax)
            {
                return false;
            }
        }

        return true;
    }

    private static string? BuildOperationKey(
        IOperation operation,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
        IParameterSymbol? resultParameter,
        IReadOnlyDictionary<
            InvocationExpressionSyntax,
            TypeMapperNestedMapExpressionModel> nestedMapMappings,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        AppendOperation(
            builder,
            operation,
            sourceParameter,
            previousParameter,
            resultParameter,
            nestedMapMappings,
            cancellationToken);
        return builder.Length == 0
            ? null
            : builder.ToString();
    }

    private static void AppendOperation(
        StringBuilder builder,
        IOperation operation,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
        IParameterSymbol? resultParameter,
        IReadOnlyDictionary<
            InvocationExpressionSyntax,
            TypeMapperNestedMapExpressionModel> nestedMapMappings,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        operation = UnwrapTransparentOperation(operation);
        builder.Append('[');
        builder.Append(operation.Kind);
        builder.Append('|');
        AppendType(
            builder,
            DeclarativeNestedMapExpression.GetEffectiveType(
                operation,
                fallbackType: null,
                nestedMapMappings));

        if (operation.ConstantValue is { HasValue: true } constant)
        {
            builder.Append("|constant:");
            AppendConstant(builder, constant.Value);
        }

        switch (operation)
        {
            case IParameterReferenceOperation parameter:
                builder.Append("|symbol:");
                builder.Append(
                    BuildSymbolKey(
                        parameter.Parameter,
                        sourceParameter,
                        previousParameter,
                        resultParameter));
                break;

            case ILocalReferenceOperation local:
                builder.Append("|symbol:");
                builder.Append(
                    BuildSymbolKey(
                        local.Local,
                        sourceParameter,
                        previousParameter,
                        resultParameter));
                break;

            case IInvocationOperation invocation:
                if (invocation.Syntax is
                        InvocationExpressionSyntax invocationSyntax &&
                    nestedMapMappings.TryGetValue(
                        invocationSyntax,
                        out var nestedMap))
                {
                    builder.Append("|nested-map:");
                    builder.Append(
                        nestedMap.Operation ==
                        DeclarativeNestedMapOperation.Update
                            ? "Update"
                            : "Create");
                    builder.Append("|source:");
                    AppendType(builder, nestedMap.SourceType);
                    builder.Append("|destination:");
                    AppendType(builder, nestedMap.DestinationType);

                    if (nestedMap.InferredSourceMemberName is
                        { } inferredSourceMember)
                    {
                        builder.Append("|inferred-source:");
                        builder.Append(inferredSourceMember);
                    }

                    if (nestedMap.GeneratedDestinationExpression is
                        { } generatedDestination)
                    {
                        builder.Append("|generated-destination:");
                        builder.Append(generatedDestination);
                    }

                    builder.Append("|guard-destination:");
                    builder.Append(nestedMap.GuardNullDestination);
                }
                else
                {
                    AppendSymbol(builder, invocation.TargetMethod);
                }
                break;

            case IObjectCreationOperation creation:
                AppendSymbol(builder, creation.Constructor);
                break;

            case IMemberReferenceOperation member:
                AppendSymbol(builder, member.Member);
                break;

            case IArgumentOperation argument:
                builder.Append("|argument:");
                builder.Append(argument.ArgumentKind);
                builder.Append(':');
                builder.Append(argument.Parameter?.Ordinal ?? -1);
                builder.Append(':');
                builder.Append(argument.Parameter?.RefKind);
                break;

            case IConversionOperation conversion:
                builder.Append("|conversion:");
                builder.Append(conversion.Conversion.IsIdentity);
                builder.Append(':');
                builder.Append(conversion.Conversion.IsNumeric);
                builder.Append(':');
                builder.Append(conversion.Conversion.IsReference);
                builder.Append(':');
                builder.Append(conversion.IsChecked);
                AppendSymbol(builder, conversion.OperatorMethod);
                break;

            case IBinaryOperation binary:
                builder.Append("|binary:");
                builder.Append(binary.OperatorKind);
                builder.Append(':');
                builder.Append(binary.IsLifted);
                builder.Append(':');
                builder.Append(binary.IsChecked);
                AppendSymbol(builder, binary.OperatorMethod);
                break;

            case IUnaryOperation unary:
                builder.Append("|unary:");
                builder.Append(unary.OperatorKind);
                builder.Append(':');
                builder.Append(unary.IsLifted);
                builder.Append(':');
                builder.Append(unary.IsChecked);
                AppendSymbol(builder, unary.OperatorMethod);
                break;

            case IInstanceReferenceOperation instance:
                builder.Append("|instance:");
                builder.Append(instance.ReferenceKind);
                break;
        }

        foreach (var child in operation.ChildOperations)
        {
            AppendOperation(
                builder,
                child,
                sourceParameter,
                previousParameter,
                resultParameter,
                nestedMapMappings,
                cancellationToken);
        }

        builder.Append(']');
    }

    private static string BuildSymbolKey(
        ISymbol symbol,
        IParameterSymbol sourceParameter,
        IParameterSymbol? previousParameter,
        IParameterSymbol? resultParameter)
    {
        if (SymbolEqualityComparer.Default.Equals(
                symbol,
                sourceParameter))
        {
            return "parameter:source";
        }

        if (previousParameter is not null &&
            SymbolEqualityComparer.Default.Equals(
                symbol,
                previousParameter))
        {
            return "parameter:previous";
        }

        if (resultParameter is not null &&
            SymbolEqualityComparer.Default.Equals(
                symbol,
                resultParameter))
        {
            return "parameter:result";
        }

        var builder = new StringBuilder();
        builder.Append(symbol.Kind);
        AppendSegment(
            builder,
            symbol.ToDisplayString(
                DependencySymbolFormat));
        AppendSegment(
            builder,
            symbol.ContainingAssembly?.Identity.ToString());

        if (symbol is ILocalSymbol or IParameterSymbol or IRangeVariableSymbol)
        {
            foreach (var location in symbol.Locations.Where(
                         static location => location.IsInSource))
            {
                AppendSegment(
                    builder,
                    location.SourceTree?.FilePath);
                builder.Append(location.SourceSpan.Start);
                builder.Append(':');
                builder.Append(location.SourceSpan.Length);
            }
        }

        return builder.ToString();
    }

    private static void AppendSymbol(
        StringBuilder builder,
        ISymbol? symbol)
    {
        if (symbol is null)
        {
            return;
        }

        builder.Append("|symbol:");
        builder.Append(symbol.Kind);
        AppendSegment(
            builder,
            symbol.ToDisplayString(
                DependencySymbolFormat));
        AppendSegment(
            builder,
            symbol.ContainingAssembly?.Identity.ToString());
    }

    private static void AppendType(
        StringBuilder builder,
        ITypeSymbol? type)
    {
        if (type is null)
        {
            builder.Append("<none>");
            return;
        }

        AppendSegment(
            builder,
            type.ToDisplayString(
                SymbolDisplayFormats.FullyQualifiedNullable));
        AppendSegment(
            builder,
            type.ContainingAssembly?.Identity.ToString());
    }

    private static void AppendConstant(
        StringBuilder builder,
        object? value)
    {
        if (value is null)
        {
            builder.Append("null");
            return;
        }

        AppendSegment(builder, value.GetType().FullName);
        AppendSegment(
            builder,
            value is IFormattable formattable
                ? formattable.ToString(
                    format: null,
                    CultureInfo.InvariantCulture)
                : value.ToString());
    }

    private static void AppendSegment(
        StringBuilder builder,
        string? value)
    {
        if (value is null)
        {
            builder.Append("-1:");
            return;
        }

        builder.Append(value.Length);
        builder.Append(':');
        builder.Append(value);
    }

    private readonly record struct DependencyCandidate(
        ExpressionSyntax Syntax,
        string Key,
        string ValueTypeName,
        bool CanMaterialize,
        SyntaxAnnotation Annotation);

    private readonly record struct RewrittenCandidate(
        DependencyCandidate Candidate,
        ExpressionSyntax Syntax);

    private sealed class SyntaxNodeReferenceComparer
        : IEqualityComparer<SyntaxNode>,
          IEqualityComparer<ExpressionSyntax>
    {
        public static SyntaxNodeReferenceComparer Instance { get; } = new();

        public static IEqualityComparer<ExpressionSyntax>
            ExpressionInstance => Instance;

        public bool Equals(SyntaxNode? x, SyntaxNode? y) =>
            ReferenceEquals(x, y);

        public int GetHashCode(SyntaxNode obj) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);

        public bool Equals(ExpressionSyntax? x, ExpressionSyntax? y) =>
            ReferenceEquals(x, y);

        public int GetHashCode(ExpressionSyntax obj) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
