using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DeclarativeControlFlowPlanner
{
    private const string UnsupportedBlockMessage =
        "Declarative plan contains a statement that is not supported.";

    private const string UnsupportedCaptureMessage =
        "Declarative plan contains a capture that cannot be transferred " +
        "to the generated mapper.";

    private const string MemberMetadataName =
        "Morphant.Members.Member`1";

    private const string ConstructorParameterMetadataName =
        "Morphant.Members.ConstructorParameter`1";

    private const string MemberMarkerMetadataName =
        "Morphant.Markers.MemberMarker";

    private const string TypeMapperMetadataName =
        "Morphant.TypeMapper";

    public static DeclarativeControlFlowBuildResult? Build(
        LambdaExpressionSyntax lambda,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        ImmutableArray<LocalDeclarationStatementSyntax>
            localDeclarations;
        ExpressionSyntax? resultExpression = null;
        var statementBlock = false;

        if (lambda.ExpressionBody is
                 { } expressionBody)
        {
            localDeclarations = [];
            resultExpression = expressionBody;
        }
        else if (lambda.Block is { } block &&
                 TryCollectSupportedLocalDeclarations(
                     block.Statements,
                     out localDeclarations))
        {
            statementBlock = true;
        }
        else
        {
            return new UnsupportedDeclarativeControlFlow(
                UnsupportedBlockMessage);
        }

        var convertedResultType =
            (semanticModel.GetTypeInfo(
                    lambda,
                    cancellationToken)
                .ConvertedType as INamedTypeSymbol)?
            .DelegateInvokeMethod?
            .ReturnType;
        var expressionResultType =
            resultExpression is not null
                ? semanticModel.GetTypeInfo(
                        resultExpression,
                        cancellationToken)
                    .Type
                : EnumerateReturnExpressions(
                        lambda.Block!.Statements)
                    .Select(expression =>
                        semanticModel.GetTypeInfo(
                                expression,
                                cancellationToken)
                            .Type)
                    .FirstOrDefault(static type =>
                        type is
                        {
                            TypeKind: not TypeKind.Error
                        });
        var declarativeResultType =
            convertedResultType is
                {
                    TypeKind: not TypeKind.Error
                }
                ? convertedResultType
                : expressionResultType is
                    {
                        TypeKind: not TypeKind.Error
                    }
                    ? expressionResultType
                    : null;
        var localInitializers =
            new Dictionary<ISymbol, ExpressionSyntax>(
                SymbolEqualityComparer.Default);
        var dslLocals =
            new HashSet<ISymbol>(
                SymbolEqualityComparer.Default);
        var dslConditionPlaceholders =
            new Dictionary<ISymbol, string>(
                SymbolEqualityComparer.Default);
        var runtimeLocalPlaceholders =
            new Dictionary<ISymbol, string>(
                SymbolEqualityComparer.Default);
        var declarationRuntimeLocalPlaceholders =
            new Dictionary<ISymbol, string>(
                SymbolEqualityComparer.Default);
        var runtimeLocals =
            ImmutableArray.CreateBuilder<
                DeclarativeRuntimeLocalSyntax>();
        var boundLocals =
            ImmutableArray.CreateBuilder<
                DeclarativeBoundLocalSyntax>();
        var allLocals =
            new HashSet<ISymbol>(
                SymbolEqualityComparer.Default);
        var reservedPlaceholderNames =
            new HashSet<string>(
                lambda.DescendantTokens()
                    .Where(static token =>
                        token.IsKind(
                            SyntaxKind.IdentifierToken))
                    .Select(static token =>
                        token.ValueText),
                StringComparer.Ordinal);
        var placeholderOrdinal = 0;

        foreach (var declaration in localDeclarations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!declaration.UsingKeyword.IsKind(
                    SyntaxKind.None) ||
                declaration.Declaration.Variables.Count == 0)
            {
                return new UnsupportedDeclarativeControlFlow(
                    UnsupportedBlockMessage);
            }

            foreach (var variable in
                     declaration.Declaration.Variables)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (variable.Initializer?.Value is not
                        { } initializer ||
                    semanticModel.GetDeclaredSymbol(
                        variable,
                        cancellationToken) is not
                        ILocalSymbol local)
                {
                    return new UnsupportedDeclarativeControlFlow(
                        UnsupportedBlockMessage);
                }

                allLocals.Add(local);
                localInitializers.Add(local, initializer);

                var isDslLocal =
                    IsDslLocalType(
                         local.Type,
                         declarativeResultType) ||
                     ContainsDslLocalInitializer(
                         initializer,
                         declarativeResultType,
                         dslLocals,
                         semanticModel,
                         cancellationToken);

                if (isDslLocal)
                {
                    dslLocals.Add(local);

                    var unwrappedInitializer =
                        UnwrapParentheses(initializer);
                    var selector = unwrappedInitializer switch
                    {
                        ConditionalExpressionSyntax conditional =>
                            conditional.Condition,
                        SwitchExpressionSyntax switchExpression =>
                            switchExpression.GoverningExpression,
                        _ => null
                    };

                    if (selector is not null)
                    {
                        var placeholder =
                            AllocatePlaceholder(
                                ref placeholderOrdinal,
                                reservedPlaceholderNames);

                        dslConditionPlaceholders.Add(
                            local,
                            placeholder);
                        runtimeLocals.Add(
                            new DeclarativeRuntimeLocalSyntax(
                                placeholder,
                                local.Name,
                                "var",
                                selector,
                                IsConst: false,
                                CanReuseForSwitchFallback: true));
                        declarationRuntimeLocalPlaceholders.Add(
                            local,
                            placeholder);
                    }

                    continue;
                }

                var runtimePlaceholder =
                    AllocatePlaceholder(
                        ref placeholderOrdinal,
                        reservedPlaceholderNames);
                var declarationType =
                    declaration.Declaration.Type.IsVar
                        ? "var"
                        : TypeMapperMappingTypePolicy
                            .GetGeneratedTypeName(
                                local.Type.WithNullableAnnotation(
                                    local.NullableAnnotation));
                var isConst = declaration.Modifiers.Any(
                    SyntaxKind.ConstKeyword);

                runtimeLocalPlaceholders.Add(
                    local,
                    runtimePlaceholder);
                runtimeLocals.Add(
                    new DeclarativeRuntimeLocalSyntax(
                        runtimePlaceholder,
                        local.Name,
                        declarationType,
                        initializer,
                        isConst,
                        isConst ||
                        CanReuseForSwitchFallback(
                            local,
                            lambda,
                            semanticModel,
                            cancellationToken)));
                declarationRuntimeLocalPlaceholders.Add(
                    local,
                    runtimePlaceholder);
            }
        }

        foreach (var designation in
                 EnumeratePatternVariableDesignations(lambda))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (semanticModel.GetDeclaredSymbol(
                    designation,
                    cancellationToken) is not
                    ILocalSymbol local ||
                runtimeLocalPlaceholders.ContainsKey(local))
            {
                continue;
            }

            var placeholder = AllocatePlaceholder(
                ref placeholderOrdinal,
                reservedPlaceholderNames);

            allLocals.Add(local);
            runtimeLocalPlaceholders.Add(
                local,
                placeholder);
            boundLocals.Add(
                new DeclarativeBoundLocalSyntax(
                    placeholder,
                    local.Name));
        }

        var transferableExpressions =
            statementBlock
                ? EnumerateStatementExpressions(
                    lambda.Block!.Statements)
                : localInitializers.Values.Append(
                    resultExpression!);

        if (ContainsUnsupportedCapture(
                transferableExpressions,
                allLocals,
                semanticModel,
                cancellationToken))
        {
            return new UnsupportedDeclarativeControlFlow(
                UnsupportedCaptureMessage);
        }

        DeclarativeControlFlowSyntaxNode? root;

        if (statementBlock)
        {
            if (!TryBuildStatementList(
                    lambda.Block!.Statements,
                    continuation: null,
                    declarativeResultType,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    declarationRuntimeLocalPlaceholders,
                    semanticModel,
                    cancellationToken,
                    out root))
            {
                return new UnsupportedDeclarativeControlFlow(
                    UnsupportedBlockMessage);
            }
        }
        else
        {
            root = ResolveDeclarativeExpression(
                    resultExpression!,
                    declarativeResultType,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken,
                    new HashSet<ISymbol>(
                        SymbolEqualityComparer.Default));

            if (root is not null)
            {
                root = WrapRuntimeLocalDeclarations(
                    localDeclarations,
                    root,
                    declarationRuntimeLocalPlaceholders,
                    semanticModel,
                    cancellationToken);
            }
        }

        if (root is not null)
        {
            root = ExpandMemberConditions(
                root,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken);
        }

        if (root is null)
        {
            return null;
        }

        foreach (var placeholder in dslConditionPlaceholders)
        {
            runtimeLocalPlaceholders.Add(
                placeholder.Key,
                placeholder.Value);
        }

        return new DeclarativeControlFlowProgram(
            root,
            runtimeLocals.ToImmutable(),
            runtimeLocalPlaceholders,
            boundLocals.ToImmutable());
    }

    private static bool CanReuseForSwitchFallback(
        ILocalSymbol local,
        LambdaExpressionSyntax lambda,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var identifier in lambda
                     .DescendantNodes()
                     .OfType<IdentifierNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SymbolEqualityComparer.Default.Equals(
                    semanticModel.GetSymbolInfo(
                            identifier,
                            cancellationToken)
                        .Symbol,
                    local))
            {
                continue;
            }

            var ancestors = identifier.Ancestors()
                .TakeWhile(node =>
                    !ReferenceEquals(node, lambda))
                .ToArray();

            if (ancestors.Any(node =>
                    node is AssignmentExpressionSyntax assignment &&
                    assignment.Left.Span.Contains(identifier.Span) ||
                    node is PrefixUnaryExpressionSyntax prefix &&
                    (prefix.IsKind(
                         SyntaxKind.PreIncrementExpression) ||
                     prefix.IsKind(
                         SyntaxKind.PreDecrementExpression)) &&
                    prefix.Operand.Span.Contains(identifier.Span) ||
                    node is PostfixUnaryExpressionSyntax postfix &&
                    (postfix.IsKind(
                         SyntaxKind.PostIncrementExpression) ||
                     postfix.IsKind(
                         SyntaxKind.PostDecrementExpression)) &&
                    postfix.Operand.Span.Contains(identifier.Span) ||
                    node is ArgumentSyntax argument &&
                    (argument.RefKindKeyword.IsKind(
                         SyntaxKind.RefKeyword) ||
                     argument.RefKindKeyword.IsKind(
                         SyntaxKind.OutKeyword)) &&
                    argument.Expression.Span.Contains(
                        identifier.Span) ||
                    node is RefExpressionSyntax refExpression &&
                    refExpression.Expression.Span.Contains(
                        identifier.Span)))
            {
                return false;
            }

            if (ancestors
                .OfType<MemberAccessExpressionSyntax>()
                .Any(memberAccess =>
                    memberAccess.Expression.Span.Contains(
                        identifier.Span) &&
                    (!local.Type.IsReferenceType ||
                     memberAccess.Parent is
                         InvocationExpressionSyntax)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryCollectSupportedLocalDeclarations(
        SyntaxList<StatementSyntax> statements,
        out ImmutableArray<LocalDeclarationStatementSyntax>
            localDeclarations)
    {
        var result =
            ImmutableArray.CreateBuilder<
                LocalDeclarationStatementSyntax>();

        foreach (var statement in statements)
        {
            if (!TryCollectSupportedLocalDeclarations(
                    statement,
                    result))
            {
                localDeclarations = default;
                return false;
            }
        }

        localDeclarations = result.ToImmutable();
        return true;
    }

    private static bool TryCollectSupportedLocalDeclarations(
        StatementSyntax statement,
        ImmutableArray<LocalDeclarationStatementSyntax>.Builder
            localDeclarations)
    {
        switch (statement)
        {
            case LocalDeclarationStatementSyntax declaration
                when declaration.UsingKeyword.IsKind(
                         SyntaxKind.None) &&
                     declaration.Declaration.Type is not
                         RefTypeSyntax &&
                     declaration.Declaration.Variables.Count > 0 &&
                     declaration.Declaration.Variables.All(
                         static variable =>
                             variable.Initializer is not null):
                localDeclarations.Add(declaration);
                return true;

            case BlockSyntax block:
                foreach (var nestedStatement in
                         block.Statements)
                {
                    if (!TryCollectSupportedLocalDeclarations(
                            nestedStatement,
                            localDeclarations))
                    {
                        return false;
                    }
                }

                return true;

            case IfStatementSyntax ifStatement:
                return TryCollectSupportedLocalDeclarations(
                           ifStatement.Statement,
                           localDeclarations) &&
                       (ifStatement.Else is null ||
                        TryCollectSupportedLocalDeclarations(
                            ifStatement.Else.Statement,
                            localDeclarations));

            case SwitchStatementSyntax switchStatement:
                foreach (var section in
                         switchStatement.Sections)
                {
                    foreach (var nestedStatement in
                             section.Statements)
                    {
                        if (!TryCollectSupportedLocalDeclarations(
                                nestedStatement,
                                localDeclarations))
                        {
                            return false;
                        }
                    }
                }

                return true;

            case ReturnStatementSyntax
                {
                    Expression: not null
                }:
            case ThrowStatementSyntax
                {
                    Expression: not null
                }:
                return true;

            default:
                return false;
        }
    }

    private static IEnumerable<ExpressionSyntax>
        EnumerateReturnExpressions(
            SyntaxList<StatementSyntax> statements)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case ReturnStatementSyntax
                    {
                        Expression: { } expression
                    }:
                    yield return expression;
                    break;

                case BlockSyntax block:
                    foreach (var nestedExpression in
                             EnumerateReturnExpressions(
                                 block.Statements))
                    {
                        yield return nestedExpression;
                    }

                    break;

                case IfStatementSyntax ifStatement:
                    foreach (var nestedExpression in
                             EnumerateReturnExpressions(
                                 SyntaxFactory.SingletonList(
                                     ifStatement.Statement)))
                    {
                        yield return nestedExpression;
                    }

                    if (ifStatement.Else is { } @else)
                    {
                        foreach (var nestedExpression in
                                 EnumerateReturnExpressions(
                                     SyntaxFactory.SingletonList(
                                         @else.Statement)))
                        {
                            yield return nestedExpression;
                        }
                    }

                    break;

                case SwitchStatementSyntax switchStatement:
                    foreach (var section in
                             switchStatement.Sections)
                    {
                        foreach (var nestedExpression in
                                 EnumerateReturnExpressions(
                                     section.Statements))
                        {
                            yield return nestedExpression;
                        }
                    }

                    break;
            }
        }
    }

    private static IEnumerable<ExpressionSyntax>
        EnumerateStatementExpressions(
            SyntaxList<StatementSyntax> statements)
    {
        foreach (var statement in statements)
        {
            foreach (var expression in
                     EnumerateStatementExpressions(statement))
            {
                yield return expression;
            }
        }
    }

    private static IEnumerable<ExpressionSyntax>
        EnumerateStatementExpressions(
            StatementSyntax statement)
    {
        switch (statement)
        {
            case LocalDeclarationStatementSyntax declaration:
                foreach (var variable in
                         declaration.Declaration.Variables)
                {
                    if (variable.Initializer?.Value is
                        { } initializer)
                    {
                        yield return initializer;
                    }
                }

                yield break;

            case BlockSyntax block:
                foreach (var expression in
                         EnumerateStatementExpressions(
                             block.Statements))
                {
                    yield return expression;
                }

                yield break;

            case IfStatementSyntax ifStatement:
                yield return ifStatement.Condition;

                foreach (var expression in
                         EnumerateStatementExpressions(
                             ifStatement.Statement))
                {
                    yield return expression;
                }

                if (ifStatement.Else is { } @else)
                {
                    foreach (var expression in
                             EnumerateStatementExpressions(
                                 @else.Statement))
                    {
                        yield return expression;
                    }
                }

                yield break;

            case SwitchStatementSyntax switchStatement:
                yield return switchStatement.Expression;

                foreach (var section in
                         switchStatement.Sections)
                {
                    foreach (var label in section.Labels)
                    {
                        switch (label)
                        {
                            case CaseSwitchLabelSyntax valueLabel:
                                yield return valueLabel.Value;
                                break;

                            case CasePatternSwitchLabelSyntax
                                {
                                    WhenClause.Condition:
                                        { } whenCondition
                                }:
                                yield return whenCondition;
                                break;
                        }
                    }

                    foreach (var expression in
                             EnumerateStatementExpressions(
                                 section.Statements))
                    {
                        yield return expression;
                    }
                }

                yield break;

            case ReturnStatementSyntax
                {
                    Expression: { } returnExpression
                }:
                yield return returnExpression;
                yield break;

            case ThrowStatementSyntax
                {
                    Expression: { } throwExpression
                }:
                yield return throwExpression;
                yield break;
        }
    }

    private static bool TryBuildStatementList(
        SyntaxList<StatementSyntax> statements,
        DeclarativeControlFlowSyntaxNode? continuation,
        ITypeSymbol? declarativeResultType,
        IReadOnlyDictionary<ISymbol, ExpressionSyntax>
            localInitializers,
        HashSet<ISymbol> dslLocals,
        IReadOnlyDictionary<ISymbol, string>
            dslConditionPlaceholders,
        IReadOnlyDictionary<ISymbol, string>
            declarationRuntimeLocalPlaceholders,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out DeclarativeControlFlowSyntaxNode? root)
    {
        root = continuation;

        for (var index = statements.Count - 1;
             index >= 0;
             index--)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (statements[index])
            {
                case ReturnStatementSyntax
                    {
                        Expression: { } returnExpression
                    }:
                    root = ResolveDeclarativeExpression(
                        returnExpression,
                        declarativeResultType,
                        localInitializers,
                        dslLocals,
                        dslConditionPlaceholders,
                        semanticModel,
                        cancellationToken,
                        new HashSet<ISymbol>(
                            SymbolEqualityComparer.Default));

                    if (root is null)
                    {
                        return false;
                    }

                    break;

                case ThrowStatementSyntax
                    {
                        Expression: { } throwExpression
                    }:
                    root = new DeclarativeThrowSyntaxNode(
                        throwExpression);
                    break;

                case LocalDeclarationStatementSyntax declaration:
                    if (root is null)
                    {
                        return false;
                    }

                    root = WrapRuntimeLocalDeclaration(
                        declaration,
                        root,
                        declarationRuntimeLocalPlaceholders,
                        semanticModel,
                        cancellationToken);
                    break;

                case BlockSyntax block:
                    if (!TryBuildStatementList(
                            block.Statements,
                            root,
                            declarativeResultType,
                            localInitializers,
                            dslLocals,
                            dslConditionPlaceholders,
                            declarationRuntimeLocalPlaceholders,
                            semanticModel,
                            cancellationToken,
                            out root))
                    {
                        return false;
                    }

                    break;

                case IfStatementSyntax ifStatement:
                    if (!TryBuildEmbeddedStatement(
                            ifStatement.Statement,
                            root,
                            declarativeResultType,
                            localInitializers,
                            dslLocals,
                            dslConditionPlaceholders,
                            declarationRuntimeLocalPlaceholders,
                            semanticModel,
                            cancellationToken,
                            out var whenTrue) ||
                        !TryBuildElseStatement(
                            ifStatement.Else,
                            root,
                            declarativeResultType,
                            localInitializers,
                            dslLocals,
                            dslConditionPlaceholders,
                            declarationRuntimeLocalPlaceholders,
                            semanticModel,
                            cancellationToken,
                            out var whenFalse) ||
                        whenTrue is null ||
                        whenFalse is null)
                    {
                        return false;
                    }

                    root = new DeclarativeConditionalSyntaxNode(
                        ifStatement.Condition,
                        whenTrue,
                        whenFalse);
                    break;

                case SwitchStatementSyntax switchStatement:
                    if (!TryBuildSwitchStatement(
                            switchStatement,
                            root,
                            declarativeResultType,
                            localInitializers,
                            dslLocals,
                            dslConditionPlaceholders,
                            declarationRuntimeLocalPlaceholders,
                            semanticModel,
                            cancellationToken,
                            out root))
                    {
                        return false;
                    }

                    break;

                default:
                    return false;
            }
        }

        return root is not null;
    }

    private static bool TryBuildEmbeddedStatement(
        StatementSyntax statement,
        DeclarativeControlFlowSyntaxNode? continuation,
        ITypeSymbol? declarativeResultType,
        IReadOnlyDictionary<ISymbol, ExpressionSyntax>
            localInitializers,
        HashSet<ISymbol> dslLocals,
        IReadOnlyDictionary<ISymbol, string>
            dslConditionPlaceholders,
        IReadOnlyDictionary<ISymbol, string>
            declarationRuntimeLocalPlaceholders,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out DeclarativeControlFlowSyntaxNode? root)
    {
        var statements = statement is BlockSyntax block
            ? block.Statements
            : SyntaxFactory.SingletonList(statement);

        return TryBuildStatementList(
            statements,
            continuation,
            declarativeResultType,
            localInitializers,
            dslLocals,
            dslConditionPlaceholders,
            declarationRuntimeLocalPlaceholders,
            semanticModel,
            cancellationToken,
            out root);
    }

    private static bool TryBuildElseStatement(
        ElseClauseSyntax? @else,
        DeclarativeControlFlowSyntaxNode? continuation,
        ITypeSymbol? declarativeResultType,
        IReadOnlyDictionary<ISymbol, ExpressionSyntax>
            localInitializers,
        HashSet<ISymbol> dslLocals,
        IReadOnlyDictionary<ISymbol, string>
            dslConditionPlaceholders,
        IReadOnlyDictionary<ISymbol, string>
            declarationRuntimeLocalPlaceholders,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out DeclarativeControlFlowSyntaxNode? root)
    {
        if (@else is null)
        {
            root = continuation;
            return true;
        }

        return TryBuildEmbeddedStatement(
            @else.Statement,
            continuation,
            declarativeResultType,
            localInitializers,
            dslLocals,
            dslConditionPlaceholders,
            declarationRuntimeLocalPlaceholders,
            semanticModel,
            cancellationToken,
            out root);
    }

    private static bool TryBuildSwitchStatement(
        SwitchStatementSyntax switchStatement,
        DeclarativeControlFlowSyntaxNode? continuation,
        ITypeSymbol? declarativeResultType,
        IReadOnlyDictionary<ISymbol, ExpressionSyntax>
            localInitializers,
        HashSet<ISymbol> dslLocals,
        IReadOnlyDictionary<ISymbol, string>
            dslConditionPlaceholders,
        IReadOnlyDictionary<ISymbol, string>
            declarationRuntimeLocalPlaceholders,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out DeclarativeControlFlowSyntaxNode? root)
    {
        var sections =
            ImmutableArray.CreateBuilder<
                DeclarativeSwitchSectionSyntax>();
        var hasDefault = false;

        foreach (var section in switchStatement.Sections)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryBuildStatementList(
                    section.Statements,
                    continuation: null,
                    declarativeResultType,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    declarationRuntimeLocalPlaceholders,
                    semanticModel,
                    cancellationToken,
                    out var branch) ||
                branch is null)
            {
                root = null;
                return false;
            }

            var labels =
                ImmutableArray.CreateBuilder<
                    DeclarativeSwitchLabelSyntax>();

            foreach (var label in section.Labels)
            {
                if (!TryBuildSwitchLabel(
                        label,
                        out var switchLabel))
                {
                    root = null;
                    return false;
                }

                hasDefault |=
                    switchLabel.Kind ==
                    DeclarativeSwitchLabelKind.Default;
                labels.Add(switchLabel);
            }

            if (labels.Count == 0)
            {
                root = null;
                return false;
            }

            sections.Add(
                new DeclarativeSwitchSectionSyntax(
                    labels.ToImmutable(),
                    branch));
        }

        if (sections.Count == 0 ||
            !hasDefault && continuation is null)
        {
            root = null;
            return false;
        }

        root = new DeclarativeSwitchSyntaxNode(
            switchStatement.Expression,
            sections.ToImmutable(),
            hasDefault
                ? null
                : continuation);
        return true;
    }

    private static bool TryBuildSwitchLabel(
        SwitchLabelSyntax label,
        out DeclarativeSwitchLabelSyntax switchLabel)
    {
        switch (label)
        {
            case DefaultSwitchLabelSyntax:
                switchLabel = new DeclarativeSwitchLabelSyntax(
                    DeclarativeSwitchLabelKind.Default,
                    Value: null,
                    Pattern: null,
                    WhenCondition: null);
                return true;

            case CaseSwitchLabelSyntax valueLabel:
                switchLabel = new DeclarativeSwitchLabelSyntax(
                    DeclarativeSwitchLabelKind.Value,
                    valueLabel.Value,
                    Pattern: null,
                    WhenCondition: null);
                return true;

            case CasePatternSwitchLabelSyntax patternLabel:
                switchLabel = new DeclarativeSwitchLabelSyntax(
                    DeclarativeSwitchLabelKind.Pattern,
                    Value: null,
                    patternLabel.Pattern,
                    patternLabel.WhenClause?.Condition);
                return true;

            default:
                switchLabel = default;
                return false;
        }
    }

    private static DeclarativeControlFlowSyntaxNode
        WrapRuntimeLocalDeclarations(
            ImmutableArray<LocalDeclarationStatementSyntax>
                declarations,
            DeclarativeControlFlowSyntaxNode root,
            IReadOnlyDictionary<ISymbol, string>
                declarationRuntimeLocalPlaceholders,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
    {
        for (var index = declarations.Length - 1;
             index >= 0;
             index--)
        {
            root = WrapRuntimeLocalDeclaration(
                declarations[index],
                root,
                declarationRuntimeLocalPlaceholders,
                semanticModel,
                cancellationToken);
        }

        return root;
    }

    private static DeclarativeControlFlowSyntaxNode
        WrapRuntimeLocalDeclaration(
            LocalDeclarationStatementSyntax declaration,
            DeclarativeControlFlowSyntaxNode next,
            IReadOnlyDictionary<ISymbol, string>
                declarationRuntimeLocalPlaceholders,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
    {
        var placeholders =
            ImmutableArray.CreateBuilder<string>();

        foreach (var variable in
                 declaration.Declaration.Variables)
        {
            if (semanticModel.GetDeclaredSymbol(
                    variable,
                    cancellationToken) is { } local &&
                declarationRuntimeLocalPlaceholders.TryGetValue(
                    local,
                    out var placeholder))
            {
                placeholders.Add(placeholder);
            }
        }

        return placeholders.Count == 0
            ? next
            : new DeclarativeLocalDeclarationsSyntaxNode(
                placeholders.ToImmutable(),
                next);
    }

    private static DeclarativeControlFlowSyntaxNode?
        ResolveDeclarativeExpression(
            ExpressionSyntax expression,
            ITypeSymbol? declarativeResultType,
            IReadOnlyDictionary<ISymbol, ExpressionSyntax>
                localInitializers,
            HashSet<ISymbol> dslLocals,
            IReadOnlyDictionary<ISymbol, string>
                dslConditionPlaceholders,
            SemanticModel semanticModel,
            CancellationToken cancellationToken,
            HashSet<ISymbol> resolvingLocals)
    {
        expression = UnwrapParentheses(expression);

        if (expression is IdentifierNameSyntax identifier &&
            semanticModel.GetSymbolInfo(
                    identifier,
                    cancellationToken)
                .Symbol is { } localSymbol &&
            dslLocals.Contains(localSymbol) &&
            localInitializers.TryGetValue(
                localSymbol,
                out var localInitializer))
        {
            if (!resolvingLocals.Add(localSymbol))
            {
                return null;
            }

            DeclarativeControlFlowSyntaxNode? localResult;

            if (UnwrapParentheses(localInitializer) is
                    ConditionalExpressionSyntax conditional &&
                dslConditionPlaceholders.TryGetValue(
                    localSymbol,
                    out var conditionPlaceholder))
            {
                var whenTrue = ResolveDeclarativeExpression(
                    conditional.WhenTrue,
                    declarativeResultType,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken,
                    resolvingLocals);
                var whenFalse = ResolveDeclarativeExpression(
                    conditional.WhenFalse,
                    declarativeResultType,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken,
                    resolvingLocals);

                localResult =
                    whenTrue is null || whenFalse is null
                        ? null
                        : new DeclarativeConditionalSyntaxNode(
                            SyntaxFactory.IdentifierName(
                                conditionPlaceholder),
                            whenTrue,
                            whenFalse);
            }
            else if (UnwrapParentheses(localInitializer) is
                         SwitchExpressionSyntax localSwitchExpression &&
                     dslConditionPlaceholders.TryGetValue(
                         localSymbol,
                         out var switchPlaceholder))
            {
                localResult = ResolveDeclarativeSwitchExpression(
                    localSwitchExpression,
                    SyntaxFactory.IdentifierName(
                        switchPlaceholder),
                    declarativeResultType,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken,
                    resolvingLocals);
            }
            else
            {
                localResult = ResolveDeclarativeExpression(
                    localInitializer,
                    declarativeResultType,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken,
                    resolvingLocals);
            }

            resolvingLocals.Remove(localSymbol);
            return localResult;
        }

        if (expression is ConditionalExpressionSyntax conditionalExpression)
        {
            var whenTrue = ResolveDeclarativeExpression(
                conditionalExpression.WhenTrue,
                declarativeResultType,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken,
                resolvingLocals);
            var whenFalse = ResolveDeclarativeExpression(
                conditionalExpression.WhenFalse,
                declarativeResultType,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken,
                resolvingLocals);

            return whenTrue is null || whenFalse is null
                ? null
                : new DeclarativeConditionalSyntaxNode(
                conditionalExpression.Condition,
                whenTrue,
                whenFalse);
        }

        if (expression is SwitchExpressionSyntax switchExpression)
        {
            return ResolveDeclarativeSwitchExpression(
                switchExpression,
                governingExpression: null,
                declarativeResultType,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken,
                resolvingLocals);
        }

        if (expression is ThrowExpressionSyntax throwExpression)
        {
            return new DeclarativeThrowSyntaxNode(
                throwExpression.Expression);
        }

        if (expression is WithExpressionSyntax withExpression)
        {
            var baseNode = ResolveDeclarativeExpression(
                withExpression.Expression,
                declarativeResultType,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken,
                resolvingLocals);

            return baseNode is null ||
                   !TryGetMemberAssignments(
                       withExpression.Initializer,
                       out var overlay)
                ? null
                : ApplyOverlay(baseNode, overlay);
        }

        if (expression is not
                BaseObjectCreationExpressionSyntax objectCreation ||
            !IsDeclarativeObjectCreation(
                objectCreation,
                declarativeResultType,
                semanticModel,
                cancellationToken) ||
            !TryGetMemberAssignments(
                objectCreation.Initializer,
                out var assignments))
        {
            return new DeclarativeLeafSyntaxNode(
                expression,
                ObjectCreation: null,
                Arguments: [],
                MemberAssignments: []);
        }

        return new DeclarativeLeafSyntaxNode(
            DirectExpression: null,
            objectCreation,
            BuildObjectArguments(objectCreation),
            assignments);
    }

    private static DeclarativeControlFlowSyntaxNode?
        ResolveDeclarativeSwitchExpression(
            SwitchExpressionSyntax switchExpression,
            ExpressionSyntax? governingExpression,
            ITypeSymbol? declarativeResultType,
            IReadOnlyDictionary<ISymbol, ExpressionSyntax>
                localInitializers,
            HashSet<ISymbol> dslLocals,
            IReadOnlyDictionary<ISymbol, string>
                dslConditionPlaceholders,
            SemanticModel semanticModel,
            CancellationToken cancellationToken,
            HashSet<ISymbol> resolvingLocals)
    {
        var sections =
            ImmutableArray.CreateBuilder<
                DeclarativeSwitchSectionSyntax>();
        var hasCatchAll = false;

        foreach (var arm in switchExpression.Arms)
        {
            var branch = ResolveDeclarativeExpression(
                arm.Expression,
                declarativeResultType,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken,
                resolvingLocals);

            if (branch is null)
            {
                return null;
            }

            var catchAll =
                arm.WhenClause is null &&
                IsUnconditionalCatchAllPattern(
                    arm.Pattern);
            hasCatchAll |= catchAll;

            sections.Add(
                new DeclarativeSwitchSectionSyntax(
                    [
                        catchAll &&
                        arm.Pattern is DiscardPatternSyntax
                            ? new DeclarativeSwitchLabelSyntax(
                                DeclarativeSwitchLabelKind.Default,
                                Value: null,
                                Pattern: null,
                                WhenCondition: null)
                            : new DeclarativeSwitchLabelSyntax(
                                DeclarativeSwitchLabelKind.Pattern,
                                Value: null,
                                arm.Pattern,
                                arm.WhenClause?.Condition)
                    ],
                    branch));
        }

        return sections.Count == 0
            ? null
            : new DeclarativeSwitchSyntaxNode(
                governingExpression ??
                switchExpression.GoverningExpression,
                sections.ToImmutable(),
                Continuation: null,
                RequiresFallback: !hasCatchAll,
                CanPassUnmatchedValue:
                    CanPassUnmatchedSwitchValue(
                        switchExpression,
                        semanticModel,
                        cancellationToken));
    }

    private static bool IsUnconditionalCatchAllPattern(
        PatternSyntax pattern)
    {
        while (pattern is ParenthesizedPatternSyntax
               {
                   Pattern: var nested
               })
        {
            pattern = nested;
        }

        return pattern is DiscardPatternSyntax or
            VarPatternSyntax;
    }

    private static bool CanPassUnmatchedSwitchValue(
        SwitchExpressionSyntax switchExpression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        return semanticModel.GetTypeInfo(
                   switchExpression.GoverningExpression,
                   cancellationToken)
               .Type is not INamedTypeSymbol
               {
                   IsRefLikeType: true
               };
    }

    private static DeclarativeControlFlowSyntaxNode ApplyOverlay(
        DeclarativeControlFlowSyntaxNode node,
        ImmutableArray<DeclarativeMemberAssignmentSyntax> overlay)
    {
        if (node is DeclarativeThrowSyntaxNode)
        {
            return node;
        }

        if (node is DeclarativeLocalDeclarationsSyntaxNode localDeclarations)
        {
            return localDeclarations with
            {
                Next = ApplyOverlay(
                    localDeclarations.Next,
                    overlay)
            };
        }

        if (node is DeclarativeConditionalSyntaxNode conditional)
        {
            return conditional with
            {
                WhenTrue = ApplyOverlay(
                    conditional.WhenTrue,
                    overlay),
                WhenFalse = ApplyOverlay(
                    conditional.WhenFalse,
                    overlay)
            };
        }

        if (node is DeclarativeSwitchSyntaxNode switchNode)
        {
            return switchNode with
            {
                Sections = switchNode.Sections
                    .Select(section =>
                        section with
                        {
                            Branch = ApplyOverlay(
                                section.Branch,
                                overlay)
                        })
                    .ToImmutableArray(),
                Continuation = switchNode.Continuation is
                    { } continuation
                    ? ApplyOverlay(
                        continuation,
                        overlay)
                    : null
            };
        }

        var leaf = (DeclarativeLeafSyntaxNode)node;
        var result = leaf.MemberAssignments.ToList();

        foreach (var assignment in overlay)
        {
            result.RemoveAll(
                existing =>
                    StringComparer.Ordinal.Equals(
                        existing.MemberName,
                        assignment.MemberName));
            result.Add(assignment);
        }

        return leaf with
        {
            MemberAssignments = result.ToImmutableArray()
        };
    }

    private static DeclarativeControlFlowSyntaxNode?
        ExpandMemberConditions(
            DeclarativeControlFlowSyntaxNode node,
            IReadOnlyDictionary<ISymbol, ExpressionSyntax>
                localInitializers,
            HashSet<ISymbol> dslLocals,
            IReadOnlyDictionary<ISymbol, string>
                dslConditionPlaceholders,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
    {
        if (node is DeclarativeLocalDeclarationsSyntaxNode localDeclarations)
        {
            var next = ExpandMemberConditions(
                localDeclarations.Next,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken);

            return next is null
                ? null
                : localDeclarations with
                {
                    Next = next
                };
        }

        if (node is DeclarativeThrowSyntaxNode)
        {
            return node;
        }

        if (node is DeclarativeConditionalSyntaxNode conditional)
        {
            var whenTrue = ExpandMemberConditions(
                conditional.WhenTrue,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken);
            var whenFalse = ExpandMemberConditions(
                conditional.WhenFalse,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken);

            return whenTrue is null || whenFalse is null
                ? null
                : conditional with
                {
                    WhenTrue = whenTrue,
                    WhenFalse = whenFalse
                };
        }

        if (node is DeclarativeSwitchSyntaxNode switchNode)
        {
            var sections =
                ImmutableArray.CreateBuilder<
                    DeclarativeSwitchSectionSyntax>(
                    switchNode.Sections.Length);

            foreach (var section in switchNode.Sections)
            {
                var branch = ExpandMemberConditions(
                    section.Branch,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken);

                if (branch is null)
                {
                    return null;
                }

                sections.Add(
                    section with
                    {
                        Branch = branch
                    });
            }

            DeclarativeControlFlowSyntaxNode? continuation = null;

            if (switchNode.Continuation is
                    { } originalContinuation)
            {
                continuation = ExpandMemberConditions(
                    originalContinuation,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken);

                if (continuation is null)
                {
                    return null;
                }
            }

            return switchNode with
            {
                Sections = sections.ToImmutable(),
                Continuation = continuation
            };
        }

        var leaf = (DeclarativeLeafSyntaxNode)node;

        return ExpandObjectArguments(
            leaf,
            index: 0,
            [],
            localInitializers,
            dslLocals,
            dslConditionPlaceholders,
            semanticModel,
            cancellationToken);
    }

    private static DeclarativeControlFlowSyntaxNode?
        ExpandObjectArguments(
            DeclarativeLeafSyntaxNode leaf,
            int index,
            ImmutableArray<DeclarativeObjectArgumentSyntax>
                arguments,
            IReadOnlyDictionary<ISymbol, ExpressionSyntax>
                localInitializers,
            HashSet<ISymbol> dslLocals,
            IReadOnlyDictionary<ISymbol, string>
                dslConditionPlaceholders,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
    {
        if (index >= leaf.Arguments.Length)
        {
            return ExpandMemberAssignments(
                leaf with
                {
                    Arguments = arguments
                },
                index: 0,
                [],
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken);
        }

        var argument = leaf.Arguments[index];

        if (argument.MemberAssignments is { } memberAssignments)
        {
            return ExpandObjectArgumentAssignments(
                leaf,
                index,
                arguments,
                argument,
                memberAssignments,
                memberIndex: 0,
                [],
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken);
        }

        var value = ResolveMemberValue(
            argument.Value,
            localInitializers,
            dslLocals,
            dslConditionPlaceholders,
            semanticModel,
            cancellationToken,
            new HashSet<ISymbol>(
                SymbolEqualityComparer.Default),
            forceConditional: false);

        if (value is null)
        {
            return null;
        }

        return ApplyMemberValue(
            value,
            branchValue =>
                ExpandObjectArguments(
                    leaf,
                    index + 1,
                    arguments.Add(
                        argument with
                        {
                            Value = branchValue
                        }),
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken));
    }

    private static DeclarativeControlFlowSyntaxNode?
        ExpandObjectArgumentAssignments(
            DeclarativeLeafSyntaxNode leaf,
            int argumentIndex,
            ImmutableArray<DeclarativeObjectArgumentSyntax>
                arguments,
            DeclarativeObjectArgumentSyntax argument,
            ImmutableArray<DeclarativeMemberAssignmentSyntax>
                memberAssignments,
            int memberIndex,
            ImmutableArray<DeclarativeMemberAssignmentSyntax>
                expandedAssignments,
            IReadOnlyDictionary<ISymbol, ExpressionSyntax>
                localInitializers,
            HashSet<ISymbol> dslLocals,
            IReadOnlyDictionary<ISymbol, string>
                dslConditionPlaceholders,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
    {
        if (memberIndex >= memberAssignments.Length)
        {
            return ExpandObjectArguments(
                leaf,
                argumentIndex + 1,
                arguments.Add(
                    argument with
                    {
                        MemberAssignments = expandedAssignments
                    }),
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken);
        }

        var assignment = memberAssignments[memberIndex];
        var value = ResolveMemberValue(
            assignment.Value,
            localInitializers,
            dslLocals,
            dslConditionPlaceholders,
            semanticModel,
            cancellationToken,
            new HashSet<ISymbol>(
                SymbolEqualityComparer.Default),
            forceConditional: false);

        if (value is null)
        {
            return null;
        }

        return ApplyMemberValue(
            value,
            branchValue =>
                ExpandObjectArgumentAssignments(
                    leaf,
                    argumentIndex,
                    arguments,
                    argument,
                    memberAssignments,
                    memberIndex + 1,
                    expandedAssignments.Add(
                        assignment with
                        {
                            Value = branchValue
                        }),
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken));
    }

    private static DeclarativeControlFlowSyntaxNode?
        ExpandMemberAssignments(
            DeclarativeLeafSyntaxNode leaf,
            int index,
            ImmutableArray<DeclarativeMemberAssignmentSyntax>
                assignments,
            IReadOnlyDictionary<ISymbol, ExpressionSyntax>
                localInitializers,
            HashSet<ISymbol> dslLocals,
            IReadOnlyDictionary<ISymbol, string>
                dslConditionPlaceholders,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
    {
        if (index >= leaf.MemberAssignments.Length)
        {
            return leaf with
            {
                MemberAssignments = assignments
            };
        }

        var assignment = leaf.MemberAssignments[index];
        var value = ResolveMemberValue(
            assignment.Value,
            localInitializers,
            dslLocals,
            dslConditionPlaceholders,
            semanticModel,
            cancellationToken,
            new HashSet<ISymbol>(
                SymbolEqualityComparer.Default),
            forceConditional: false);

        if (value is null)
        {
            return null;
        }

        return ApplyMemberValue(
            value,
            branchValue =>
                ExpandMemberAssignments(
                    leaf,
                    index + 1,
                    assignments.Add(
                        assignment with
                        {
                            Value = branchValue
                        }),
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken));
    }

    private static DeclarativeControlFlowSyntaxNode?
        ApplyMemberValue(
            DeclarativeMemberValueSyntaxNode value,
            Func<ExpressionSyntax,
                DeclarativeControlFlowSyntaxNode?> applyLeaf)
    {
        if (value is DeclarativeMemberValueLeafSyntaxNode leaf)
        {
            return applyLeaf(leaf.Value);
        }

        if (value is DeclarativeMemberValueSwitchSyntaxNode switchValue)
        {
            var sections =
                ImmutableArray.CreateBuilder<
                    DeclarativeSwitchSectionSyntax>(
                    switchValue.Sections.Length);

            foreach (var section in switchValue.Sections)
            {
                var branch = ApplyMemberValue(
                    section.Value,
                    applyLeaf);

                if (branch is null)
                {
                    return null;
                }

                sections.Add(
                    new DeclarativeSwitchSectionSyntax(
                        [section.Label],
                        branch));
            }

            return new DeclarativeSwitchSyntaxNode(
                switchValue.GoverningExpression,
                sections.ToImmutable(),
                Continuation: null,
                switchValue.RequiresFallback,
                switchValue.CanPassUnmatchedValue);
        }

        var conditional =
            (DeclarativeMemberValueConditionalSyntaxNode)value;
        var whenTrue = ApplyMemberValue(
            conditional.WhenTrue,
            applyLeaf);
        var whenFalse = ApplyMemberValue(
            conditional.WhenFalse,
            applyLeaf);

        return whenTrue is null || whenFalse is null
            ? null
            : new DeclarativeConditionalSyntaxNode(
                conditional.Condition,
                whenTrue,
                whenFalse);
    }

    private static DeclarativeMemberValueSyntaxNode?
        ResolveMemberValue(
            ExpressionSyntax expression,
            IReadOnlyDictionary<ISymbol, ExpressionSyntax>
                localInitializers,
            HashSet<ISymbol> dslLocals,
            IReadOnlyDictionary<ISymbol, string>
                dslConditionPlaceholders,
            SemanticModel semanticModel,
            CancellationToken cancellationToken,
            HashSet<ISymbol> resolvingLocals,
            bool forceConditional)
    {
        var unwrapped = UnwrapParentheses(expression);

        if (unwrapped is IdentifierNameSyntax identifier &&
            semanticModel.GetSymbolInfo(
                    identifier,
                    cancellationToken)
                .Symbol is { } localSymbol &&
            dslLocals.Contains(localSymbol) &&
            localInitializers.TryGetValue(
                localSymbol,
                out var localInitializer))
        {
            if (!resolvingLocals.Add(localSymbol))
            {
                return null;
            }

            DeclarativeMemberValueSyntaxNode? result;

            if (UnwrapParentheses(localInitializer) is
                    ConditionalExpressionSyntax conditional &&
                dslConditionPlaceholders.TryGetValue(
                    localSymbol,
                    out var conditionPlaceholder))
            {
                var whenTrue = ResolveMemberValue(
                    conditional.WhenTrue,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken,
                    resolvingLocals,
                    forceConditional: true);
                var whenFalse = ResolveMemberValue(
                    conditional.WhenFalse,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken,
                    resolvingLocals,
                    forceConditional: true);

                result =
                    whenTrue is null || whenFalse is null
                        ? null
                        : new
                            DeclarativeMemberValueConditionalSyntaxNode(
                                SyntaxFactory.IdentifierName(
                                    conditionPlaceholder),
                                whenTrue,
                                whenFalse);
            }
            else if (UnwrapParentheses(localInitializer) is
                         SwitchExpressionSyntax localSwitchExpression &&
                     dslConditionPlaceholders.TryGetValue(
                         localSymbol,
                         out var switchPlaceholder))
            {
                result = ResolveMemberSwitchValue(
                    localSwitchExpression,
                    SyntaxFactory.IdentifierName(
                        switchPlaceholder),
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken,
                    resolvingLocals,
                    forceConditional: true);
            }
            else
            {
                result = ResolveMemberValue(
                    localInitializer,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken,
                    resolvingLocals,
                    forceConditional: true);
            }

            resolvingLocals.Remove(localSymbol);
            return result;
        }

        if (unwrapped is
            ConditionalExpressionSyntax conditionalExpression)
        {
            var whenTrue = ResolveMemberValue(
                conditionalExpression.WhenTrue,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken,
                resolvingLocals,
                forceConditional);
            var whenFalse = ResolveMemberValue(
                conditionalExpression.WhenFalse,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken,
                resolvingLocals,
                forceConditional);

            if (whenTrue is null || whenFalse is null)
            {
                return null;
            }

            if (!forceConditional &&
                !ContainsDslMemberValue(whenTrue) &&
                !ContainsDslMemberValue(whenFalse))
            {
                return new DeclarativeMemberValueLeafSyntaxNode(
                    expression,
                    IsDsl: false);
            }

            return new DeclarativeMemberValueConditionalSyntaxNode(
                conditionalExpression.Condition,
                whenTrue,
                whenFalse);
        }

        if (unwrapped is SwitchExpressionSyntax switchExpression)
        {
            return ResolveMemberSwitchValue(
                switchExpression,
                governingExpression: null,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken,
                resolvingLocals,
                forceConditional);
        }

        var isDsl = IsMemberDslExpression(
            unwrapped,
            semanticModel,
            cancellationToken);

        return new DeclarativeMemberValueLeafSyntaxNode(
            expression,
            isDsl);
    }

    private static DeclarativeMemberValueSyntaxNode?
        ResolveMemberSwitchValue(
            SwitchExpressionSyntax switchExpression,
            ExpressionSyntax? governingExpression,
            IReadOnlyDictionary<ISymbol, ExpressionSyntax>
                localInitializers,
            HashSet<ISymbol> dslLocals,
            IReadOnlyDictionary<ISymbol, string>
                dslConditionPlaceholders,
            SemanticModel semanticModel,
            CancellationToken cancellationToken,
            HashSet<ISymbol> resolvingLocals,
            bool forceConditional)
    {
        var sections =
            ImmutableArray.CreateBuilder<
                DeclarativeMemberValueSwitchSectionSyntax>();
        var hasCatchAll = false;
        var containsDsl = false;

        foreach (var arm in switchExpression.Arms)
        {
            var value = ResolveMemberValue(
                arm.Expression,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken,
                resolvingLocals,
                forceConditional);

            if (value is null)
            {
                return null;
            }

            containsDsl |= ContainsDslMemberValue(value);

            var catchAll =
                arm.WhenClause is null &&
                IsUnconditionalCatchAllPattern(
                    arm.Pattern);
            hasCatchAll |= catchAll;

            sections.Add(
                new DeclarativeMemberValueSwitchSectionSyntax(
                    catchAll &&
                    arm.Pattern is DiscardPatternSyntax
                        ? new DeclarativeSwitchLabelSyntax(
                            DeclarativeSwitchLabelKind.Default,
                            Value: null,
                            Pattern: null,
                            WhenCondition: null)
                        : new DeclarativeSwitchLabelSyntax(
                            DeclarativeSwitchLabelKind.Pattern,
                            Value: null,
                            arm.Pattern,
                            arm.WhenClause?.Condition),
                    value));
        }

        if (sections.Count == 0)
        {
            return null;
        }

        return !forceConditional &&
               !containsDsl
            ? new DeclarativeMemberValueLeafSyntaxNode(
                switchExpression,
                IsDsl: false)
            : new DeclarativeMemberValueSwitchSyntaxNode(
                governingExpression ??
                switchExpression.GoverningExpression,
                sections.ToImmutable(),
                RequiresFallback: !hasCatchAll,
                CanPassUnmatchedValue:
                    CanPassUnmatchedSwitchValue(
                        switchExpression,
                        semanticModel,
                        cancellationToken));
    }

    private static bool ContainsDslMemberValue(
        DeclarativeMemberValueSyntaxNode node)
    {
        return node switch
        {
            DeclarativeMemberValueLeafSyntaxNode leaf =>
                leaf.IsDsl,
            DeclarativeMemberValueConditionalSyntaxNode =>
                true,
            DeclarativeMemberValueSwitchSyntaxNode =>
                true,
            _ => false
        };
    }

    private static bool IsMemberDslExpression(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var typeInfo = semanticModel.GetTypeInfo(
            expression,
            cancellationToken);

        if (IsMemberMarkerType(typeInfo.Type) ||
            IsMemberMarkerType(typeInfo.ConvertedType))
        {
            return true;
        }

        var symbolInfo = semanticModel.GetSymbolInfo(
            expression,
            cancellationToken);

        if (symbolInfo.Symbol is IMethodSymbol method &&
            IsTypeMapperMemberMarkerMethod(method))
        {
            return true;
        }

        return symbolInfo.CandidateSymbols
            .OfType<IMethodSymbol>()
            .Any(IsTypeMapperMemberMarkerMethod);
    }

    private static bool IsTypeMapperMemberMarkerMethod(
        IMethodSymbol method)
    {
        return IsMemberMarkerType(method.ReturnType) &&
               StringComparer.Ordinal.Equals(
                   SymbolNameHelper.GetFullMetadataName(
                       method.ContainingType),
                   TypeMapperMetadataName);
    }

    private static bool IsMemberMarkerType(
        ITypeSymbol? type)
    {
        for (var current = type as INamedTypeSymbol;
             current is not null;
             current = current.BaseType)
        {
            if (StringComparer.Ordinal.Equals(
                    SymbolNameHelper.GetFullMetadataName(
                        current),
                    MemberMarkerMetadataName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetMemberAssignments(
        InitializerExpressionSyntax? initializer,
        out ImmutableArray<DeclarativeMemberAssignmentSyntax>
            assignments)
    {
        var result =
            ImmutableArray.CreateBuilder<
                DeclarativeMemberAssignmentSyntax>();
        var seenNames =
            new HashSet<string>(StringComparer.Ordinal);

        foreach (var expression in
                 initializer?.Expressions ?? default)
        {
            if (expression is not AssignmentExpressionSyntax
                {
                    RawKind:
                        (int)SyntaxKind.SimpleAssignmentExpression,
                    Left: IdentifierNameSyntax memberName
                } assignment ||
                !seenNames.Add(
                    memberName.Identifier.ValueText))
            {
                assignments = default;
                return false;
            }

            result.Add(
                new DeclarativeMemberAssignmentSyntax(
                    memberName.Identifier.ValueText,
                    assignment.Right));
        }

        assignments = result.ToImmutable();
        return true;
    }

    private static ImmutableArray<DeclarativeObjectArgumentSyntax>
        BuildObjectArguments(
            BaseObjectCreationExpressionSyntax objectCreation)
    {
        return (objectCreation.ArgumentList?.Arguments ??
                default)
            .Select(argument =>
            {
                ImmutableArray<DeclarativeMemberAssignmentSyntax>?
                    memberAssignments = null;

                if (UnwrapParentheses(argument.Expression) is
                        BaseObjectCreationExpressionSyntax
                        argumentObjectCreation &&
                    argumentObjectCreation.ArgumentList?.Arguments.Count
                        is null or 0 &&
                    TryGetMemberAssignments(
                        argumentObjectCreation.Initializer,
                        out var assignments))
                {
                    memberAssignments = assignments;
                }

                return new DeclarativeObjectArgumentSyntax(
                    argument,
                    argument.Expression,
                    memberAssignments);
            })
            .ToImmutableArray();
    }

    private static bool IsDeclarativeObjectCreation(
        BaseObjectCreationExpressionSyntax objectCreation,
        ITypeSymbol? declarativeResultType,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (declarativeResultType is null)
        {
            return objectCreation is
                ImplicitObjectCreationExpressionSyntax;
        }

        var createdType = semanticModel.GetTypeInfo(
                objectCreation,
                cancellationToken)
            .Type;

        return createdType is not null &&
               SymbolEqualityComparer.Default.Equals(
                   createdType,
                   declarativeResultType);
    }

    private static bool IsDslLocalType(
        ITypeSymbol type,
        ITypeSymbol? declarativeResultType)
    {
        if (declarativeResultType is
                {
                    TypeKind: not TypeKind.Error
                } &&
            type.TypeKind != TypeKind.Error &&
            SymbolEqualityComparer.Default.Equals(
                type,
                declarativeResultType))
        {
            return true;
        }

        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var metadataName =
            SymbolNameHelper.GetFullMetadataName(
                namedType.OriginalDefinition);

        if (metadataName is
            MemberMetadataName or
            ConstructorParameterMetadataName)
        {
            return true;
        }

        for (var current = namedType;
             current is not null;
             current = current.BaseType)
        {
            if (StringComparer.Ordinal.Equals(
                    SymbolNameHelper.GetFullMetadataName(
                        current),
                    MemberMarkerMetadataName))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsDslLocalInitializer(
        ExpressionSyntax expression,
        ITypeSymbol? declarativeResultType,
        HashSet<ISymbol> dslLocals,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        expression = UnwrapParentheses(expression);

        if (expression is IdentifierNameSyntax identifier &&
            semanticModel.GetSymbolInfo(
                    identifier,
                    cancellationToken)
                .Symbol is { } symbol &&
            dslLocals.Contains(symbol))
        {
            return true;
        }

        if (expression is ConditionalExpressionSyntax conditional)
        {
            return ContainsDslLocalInitializer(
                       conditional.WhenTrue,
                       declarativeResultType,
                       dslLocals,
                       semanticModel,
                       cancellationToken) ||
                   ContainsDslLocalInitializer(
                       conditional.WhenFalse,
                       declarativeResultType,
                       dslLocals,
                       semanticModel,
                       cancellationToken);
        }

        if (expression is SwitchExpressionSyntax switchExpression)
        {
            return switchExpression.Arms.Any(
                arm => ContainsDslLocalInitializer(
                    arm.Expression,
                    declarativeResultType,
                    dslLocals,
                    semanticModel,
                    cancellationToken));
        }

        if (expression is WithExpressionSyntax withExpression)
        {
            return ContainsDslLocalInitializer(
                withExpression.Expression,
                declarativeResultType,
                dslLocals,
                semanticModel,
                cancellationToken);
        }

        if (declarativeResultType is not null &&
            expression is BaseObjectCreationExpressionSyntax
                objectCreation &&
            IsDeclarativeObjectCreation(
                objectCreation,
                declarativeResultType,
                semanticModel,
                cancellationToken))
        {
            return true;
        }

        return IsMemberDslExpression(
            expression,
            semanticModel,
            cancellationToken);
    }

    private static bool ContainsUnsupportedCapture(
        IEnumerable<ExpressionSyntax> expressions,
        HashSet<ISymbol> allowedLocals,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var expressionArray = expressions.ToArray();

        foreach (var expression in expressionArray)
        {
            foreach (var identifier in expression
                         .DescendantNodesAndSelf()
                         .OfType<SimpleNameSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var symbolInfo = semanticModel.GetSymbolInfo(
                    identifier,
                    cancellationToken);
                var symbol = symbolInfo.Symbol ??
                    symbolInfo.CandidateSymbols
                        .FirstOrDefault(static candidate =>
                            candidate is IMethodSymbol
                            {
                                MethodKind:
                                    MethodKind.LocalFunction
                            }) ??
                    symbolInfo.CandidateSymbols
                        .FirstOrDefault();

                if (symbol is null &&
                    identifier.Parent is
                        InvocationExpressionSyntax
                        {
                            Expression:
                                var invocationExpression
                        } invocation &&
                    ReferenceEquals(
                        invocationExpression,
                        identifier))
                {
                    var invocationInfo =
                        semanticModel.GetSymbolInfo(
                            invocation,
                            cancellationToken);

                    symbol = invocationInfo.Symbol ??
                        invocationInfo.CandidateSymbols
                            .FirstOrDefault(static candidate =>
                                candidate is IMethodSymbol
                                {
                                    MethodKind:
                                        MethodKind.LocalFunction
                                }) ??
                        invocationInfo.CandidateSymbols
                            .FirstOrDefault();
                }

                if (IsInsideByFactoryArgument(
                        identifier,
                        semanticModel,
                        cancellationToken))
                {
                    continue;
                }

                if (symbol is ILocalSymbol
                    {
                        IsConst: true
                    })
                {
                    continue;
                }

                if ((symbol is ILocalSymbol local &&
                     !allowedLocals.Contains(local) &&
                     !IsDeclaredWithin(
                         local,
                         expressionArray)) ||
                    symbol is IRangeVariableSymbol ||
                    symbol is IMethodSymbol
                    {
                        MethodKind:
                            MethodKind.LocalFunction
                    })
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsInsideByFactoryArgument(
        SimpleNameSyntax identifier,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        foreach (var argument in identifier
                     .Ancestors()
                     .OfType<ArgumentSyntax>())
        {
            if (argument.Parent is not ArgumentListSyntax
                {
                    Parent:
                        InvocationExpressionSyntax invocation
                } ||
                semanticModel.GetSymbolInfo(
                        invocation,
                        cancellationToken)
                    .Symbol is not IMethodSymbol
                    {
                        Name: "ByFactory",
                        ContainingType: { } containingType
                    } ||
                !StringComparer.Ordinal.Equals(
                    SymbolNameHelper.GetFullMetadataName(
                        containingType),
                    TypeMapperMetadataName))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool IsDeclaredWithin(
        ISymbol symbol,
        IEnumerable<ExpressionSyntax> expressions)
    {
        foreach (var reference in
                 symbol.DeclaringSyntaxReferences)
        {
            foreach (var expression in expressions)
            {
                if (ReferenceEquals(
                        reference.SyntaxTree,
                        expression.SyntaxTree) &&
                    expression.FullSpan.Contains(
                        reference.Span))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static ImmutableArray<
        SingleVariableDesignationSyntax>
        EnumeratePatternVariableDesignations(
            LambdaExpressionSyntax lambda)
    {
        var result =
            ImmutableArray.CreateBuilder<
                SingleVariableDesignationSyntax>();
        var walker =
            new PatternVariableDesignationWalker(result);

        if (lambda.Block is { } block)
        {
            walker.Visit(block);
        }
        else if (lambda.ExpressionBody is { } expression)
        {
            walker.Visit(expression);
        }

        return result.ToImmutable();
    }

    private static string AllocatePlaceholder(
        ref int ordinal,
        HashSet<string> reservedNames)
    {
        while (true)
        {
            var candidate =
                "__morphantDeclarativeLocal" +
                ordinal++.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);

            if (reservedNames.Add(candidate))
            {
                return candidate;
            }
        }
    }

    private static ExpressionSyntax UnwrapParentheses(
        ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax
               {
                   Expression: var nested
               })
        {
            expression = nested;
        }

        return expression;
    }

    private sealed class PatternVariableDesignationWalker(
        ImmutableArray<SingleVariableDesignationSyntax>.Builder
            result)
        : CSharpSyntaxWalker
    {
        public override void VisitSingleVariableDesignation(
            SingleVariableDesignationSyntax node)
        {
            if (node.Ancestors()
                .OfType<PatternSyntax>()
                .Any())
            {
                result.Add(node);
            }

            base.VisitSingleVariableDesignation(node);
        }

        public override void VisitSimpleLambdaExpression(
            SimpleLambdaExpressionSyntax node)
        {
        }

        public override void VisitParenthesizedLambdaExpression(
            ParenthesizedLambdaExpressionSyntax node)
        {
        }

        public override void VisitAnonymousMethodExpression(
            AnonymousMethodExpressionSyntax node)
        {
        }

        public override void VisitLocalFunctionStatement(
            LocalFunctionStatementSyntax node)
        {
        }
    }
}

internal abstract record DeclarativeControlFlowBuildResult;

internal sealed record UnsupportedDeclarativeControlFlow(
    string Message)
    : DeclarativeControlFlowBuildResult;

internal sealed record DeclarativeControlFlowProgram(
    DeclarativeControlFlowSyntaxNode Root,
    ImmutableArray<DeclarativeRuntimeLocalSyntax> RuntimeLocals,
    IReadOnlyDictionary<ISymbol, string> RuntimeLocalPlaceholders,
    ImmutableArray<DeclarativeBoundLocalSyntax> BoundLocals)
    : DeclarativeControlFlowBuildResult;

internal readonly record struct DeclarativeRuntimeLocalSyntax(
    string PlaceholderName,
    string PreferredName,
    string DeclarationType,
    ExpressionSyntax Initializer,
    bool IsConst,
    bool CanReuseForSwitchFallback);

internal readonly record struct DeclarativeBoundLocalSyntax(
    string PlaceholderName,
    string PreferredName);

internal abstract record DeclarativeControlFlowSyntaxNode;

internal sealed record DeclarativeLocalDeclarationsSyntaxNode(
    ImmutableArray<string> RuntimeLocalPlaceholders,
    DeclarativeControlFlowSyntaxNode Next)
    : DeclarativeControlFlowSyntaxNode;

internal sealed record DeclarativeConditionalSyntaxNode(
    ExpressionSyntax Condition,
    DeclarativeControlFlowSyntaxNode WhenTrue,
    DeclarativeControlFlowSyntaxNode WhenFalse)
    : DeclarativeControlFlowSyntaxNode;

internal sealed record DeclarativeSwitchSyntaxNode(
    ExpressionSyntax GoverningExpression,
    ImmutableArray<DeclarativeSwitchSectionSyntax> Sections,
    DeclarativeControlFlowSyntaxNode? Continuation,
    bool RequiresFallback = false,
    bool CanPassUnmatchedValue = true)
    : DeclarativeControlFlowSyntaxNode;

internal readonly record struct DeclarativeSwitchSectionSyntax(
    ImmutableArray<DeclarativeSwitchLabelSyntax> Labels,
    DeclarativeControlFlowSyntaxNode Branch);

internal readonly record struct DeclarativeSwitchLabelSyntax(
    DeclarativeSwitchLabelKind Kind,
    ExpressionSyntax? Value,
    PatternSyntax? Pattern,
    ExpressionSyntax? WhenCondition);

internal enum DeclarativeSwitchLabelKind
{
    Default,
    Value,
    Pattern
}

internal sealed record DeclarativeLeafSyntaxNode(
    ExpressionSyntax? DirectExpression,
    BaseObjectCreationExpressionSyntax? ObjectCreation,
    ImmutableArray<DeclarativeObjectArgumentSyntax> Arguments,
    ImmutableArray<DeclarativeMemberAssignmentSyntax> MemberAssignments)
    : DeclarativeControlFlowSyntaxNode;

internal sealed record DeclarativeThrowSyntaxNode(
    ExpressionSyntax Expression)
    : DeclarativeControlFlowSyntaxNode;

internal readonly record struct DeclarativeObjectArgumentSyntax(
    ArgumentSyntax Syntax,
    ExpressionSyntax Value,
    ImmutableArray<DeclarativeMemberAssignmentSyntax>? MemberAssignments);

internal readonly record struct DeclarativeMemberAssignmentSyntax(
    string MemberName,
    ExpressionSyntax Value);

internal abstract record DeclarativeMemberValueSyntaxNode;

internal sealed record DeclarativeMemberValueConditionalSyntaxNode(
    ExpressionSyntax Condition,
    DeclarativeMemberValueSyntaxNode WhenTrue,
    DeclarativeMemberValueSyntaxNode WhenFalse)
    : DeclarativeMemberValueSyntaxNode;

internal sealed record DeclarativeMemberValueSwitchSyntaxNode(
    ExpressionSyntax GoverningExpression,
    ImmutableArray<DeclarativeMemberValueSwitchSectionSyntax> Sections,
    bool RequiresFallback = false,
    bool CanPassUnmatchedValue = true)
    : DeclarativeMemberValueSyntaxNode;

internal readonly record struct
    DeclarativeMemberValueSwitchSectionSyntax(
        DeclarativeSwitchLabelSyntax Label,
        DeclarativeMemberValueSyntaxNode Value);

internal sealed record DeclarativeMemberValueLeafSyntaxNode(
    ExpressionSyntax Value,
    bool IsDsl)
    : DeclarativeMemberValueSyntaxNode;
