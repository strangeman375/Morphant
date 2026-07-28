using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TemplateControlFlowPlanner
{
    private const string MemberMetadataName =
        "Morphant.Members.Member`1";

    private const string ConstructorMemberMetadataName =
        "Morphant.Members.ConstructorMember`1";

    private const string MemberMarkerMetadataName =
        "Morphant.Markers.MemberMarker";

    private const string TypeMapperMetadataName =
        "Morphant.TypeMapper";

    public static TemplateControlFlowProgram? Build(
        LambdaExpressionSyntax lambda,
        SemanticModel semanticModel,
        bool directTemplate,
        CancellationToken cancellationToken)
    {
        if (!TryGetLambdaResult(
                lambda,
                out var localDeclarations,
                out var resultExpression))
        {
            throw new NotSupportedException(
                "Template block lambdas currently support only local " +
                "variable declarations followed by a single return " +
                "statement.");
        }

        var convertedResultType =
            (semanticModel.GetTypeInfo(
                    lambda,
                    cancellationToken)
                .ConvertedType as INamedTypeSymbol)?
            .DelegateInvokeMethod?
            .ReturnType;
        var expressionResultType =
            semanticModel.GetTypeInfo(
                    resultExpression,
                    cancellationToken)
                .Type;
        var templateResultType =
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
        var runtimeLocals =
            ImmutableArray.CreateBuilder<
                TemplateRuntimeLocalSyntax>();
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
                return null;
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
                    return null;
                }

                allLocals.Add(local);
                localInitializers.Add(local, initializer);

                var isDslLocal =
                    !directTemplate &&
                    (IsDslLocalType(
                         local.Type,
                         templateResultType) ||
                     ContainsDslLocalInitializer(
                         initializer,
                         templateResultType,
                         dslLocals,
                         semanticModel,
                         cancellationToken));

                if (isDslLocal)
                {
                    dslLocals.Add(local);

                    if (UnwrapParentheses(initializer) is
                        ConditionalExpressionSyntax conditional)
                    {
                        var placeholder =
                            AllocatePlaceholder(
                                ref placeholderOrdinal,
                                reservedPlaceholderNames);

                        dslConditionPlaceholders.Add(
                            local,
                            placeholder);
                        runtimeLocals.Add(
                            new TemplateRuntimeLocalSyntax(
                                placeholder,
                                local.Name,
                                "var",
                                conditional.Condition));
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

                runtimeLocalPlaceholders.Add(
                    local,
                    runtimePlaceholder);
                runtimeLocals.Add(
                    new TemplateRuntimeLocalSyntax(
                        runtimePlaceholder,
                        local.Name,
                        declarationType,
                        initializer));
            }
        }

        if (ContainsUnsupportedCapture(
                localInitializers.Values.Append(
                    resultExpression),
                allLocals,
                semanticModel,
                cancellationToken))
        {
            return null;
        }

        TemplateControlFlowSyntaxNode? root =
            directTemplate
                ? new TemplateLeafSyntaxNode(
                    resultExpression,
                    ObjectCreation: null,
                    Arguments: [],
                    MemberAssignments: [])
                : ResolveTemplateExpression(
                    resultExpression,
                    templateResultType,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken,
                    new HashSet<ISymbol>(
                        SymbolEqualityComparer.Default));

        if (root is not null &&
            !directTemplate)
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

        return new TemplateControlFlowProgram(
            root,
            runtimeLocals.ToImmutable(),
            runtimeLocalPlaceholders);
    }

    private static bool TryGetLambdaResult(
        LambdaExpressionSyntax lambda,
        out ImmutableArray<LocalDeclarationStatementSyntax>
            localDeclarations,
        out ExpressionSyntax resultExpression)
    {
        if (lambda.ExpressionBody is { } expressionBody)
        {
            localDeclarations = [];
            resultExpression = expressionBody;
            return true;
        }

        if (lambda.Block is not { } block ||
            block.Statements.Count == 0 ||
            block.Statements[block.Statements.Count - 1] is not
                ReturnStatementSyntax
            {
                Expression: { } returnExpression
            })
        {
            localDeclarations = default;
            resultExpression = null!;
            return false;
        }

        var declarations =
            ImmutableArray.CreateBuilder<
                LocalDeclarationStatementSyntax>(
                block.Statements.Count - 1);

        for (var index = 0;
             index < block.Statements.Count - 1;
             index++)
        {
            if (block.Statements[index] is not
                LocalDeclarationStatementSyntax declaration)
            {
                localDeclarations = default;
                resultExpression = null!;
                return false;
            }

            declarations.Add(declaration);
        }

        localDeclarations = declarations.ToImmutable();
        resultExpression = returnExpression;
        return true;
    }

    private static TemplateControlFlowSyntaxNode?
        ResolveTemplateExpression(
            ExpressionSyntax expression,
            ITypeSymbol? templateResultType,
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

            TemplateControlFlowSyntaxNode? localResult;

            if (UnwrapParentheses(localInitializer) is
                    ConditionalExpressionSyntax conditional &&
                dslConditionPlaceholders.TryGetValue(
                    localSymbol,
                    out var conditionPlaceholder))
            {
                var whenTrue = ResolveTemplateExpression(
                    conditional.WhenTrue,
                    templateResultType,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken,
                    resolvingLocals);
                var whenFalse = ResolveTemplateExpression(
                    conditional.WhenFalse,
                    templateResultType,
                    localInitializers,
                    dslLocals,
                    dslConditionPlaceholders,
                    semanticModel,
                    cancellationToken,
                    resolvingLocals);

                localResult =
                    whenTrue is null || whenFalse is null
                        ? null
                        : new TemplateConditionalSyntaxNode(
                            SyntaxFactory.IdentifierName(
                                conditionPlaceholder),
                            whenTrue,
                            whenFalse);
            }
            else
            {
                localResult = ResolveTemplateExpression(
                    localInitializer,
                    templateResultType,
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
            var whenTrue = ResolveTemplateExpression(
                conditionalExpression.WhenTrue,
                templateResultType,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken,
                resolvingLocals);
            var whenFalse = ResolveTemplateExpression(
                conditionalExpression.WhenFalse,
                templateResultType,
                localInitializers,
                dslLocals,
                dslConditionPlaceholders,
                semanticModel,
                cancellationToken,
                resolvingLocals);

            return whenTrue is null || whenFalse is null
                ? null
                : new TemplateConditionalSyntaxNode(
                    conditionalExpression.Condition,
                    whenTrue,
                    whenFalse);
        }

        if (expression is WithExpressionSyntax withExpression)
        {
            var baseNode = ResolveTemplateExpression(
                withExpression.Expression,
                templateResultType,
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
            !IsTemplateObjectCreation(
                objectCreation,
                templateResultType,
                semanticModel,
                cancellationToken) ||
            !TryGetMemberAssignments(
                objectCreation.Initializer,
                out var assignments))
        {
            return null;
        }

        return new TemplateLeafSyntaxNode(
            DirectExpression: null,
            objectCreation,
            BuildObjectArguments(objectCreation),
            assignments);
    }

    private static TemplateControlFlowSyntaxNode ApplyOverlay(
        TemplateControlFlowSyntaxNode node,
        ImmutableArray<TemplateMemberAssignmentSyntax> overlay)
    {
        if (node is TemplateConditionalSyntaxNode conditional)
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

        var leaf = (TemplateLeafSyntaxNode)node;
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

    private static TemplateControlFlowSyntaxNode?
        ExpandMemberConditions(
            TemplateControlFlowSyntaxNode node,
            IReadOnlyDictionary<ISymbol, ExpressionSyntax>
                localInitializers,
            HashSet<ISymbol> dslLocals,
            IReadOnlyDictionary<ISymbol, string>
                dslConditionPlaceholders,
            SemanticModel semanticModel,
            CancellationToken cancellationToken)
    {
        if (node is TemplateConditionalSyntaxNode conditional)
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

        var leaf = (TemplateLeafSyntaxNode)node;

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

    private static TemplateControlFlowSyntaxNode?
        ExpandObjectArguments(
            TemplateLeafSyntaxNode leaf,
            int index,
            ImmutableArray<TemplateObjectArgumentSyntax>
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

    private static TemplateControlFlowSyntaxNode?
        ExpandObjectArgumentAssignments(
            TemplateLeafSyntaxNode leaf,
            int argumentIndex,
            ImmutableArray<TemplateObjectArgumentSyntax>
                arguments,
            TemplateObjectArgumentSyntax argument,
            ImmutableArray<TemplateMemberAssignmentSyntax>
                memberAssignments,
            int memberIndex,
            ImmutableArray<TemplateMemberAssignmentSyntax>
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

    private static TemplateControlFlowSyntaxNode?
        ExpandMemberAssignments(
            TemplateLeafSyntaxNode leaf,
            int index,
            ImmutableArray<TemplateMemberAssignmentSyntax>
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

    private static TemplateControlFlowSyntaxNode?
        ApplyMemberValue(
            TemplateMemberValueSyntaxNode value,
            Func<ExpressionSyntax,
                TemplateControlFlowSyntaxNode?> applyLeaf)
    {
        if (value is TemplateMemberValueLeafSyntaxNode leaf)
        {
            return applyLeaf(leaf.Value);
        }

        var conditional =
            (TemplateMemberValueConditionalSyntaxNode)value;
        var whenTrue = ApplyMemberValue(
            conditional.WhenTrue,
            applyLeaf);
        var whenFalse = ApplyMemberValue(
            conditional.WhenFalse,
            applyLeaf);

        return whenTrue is null || whenFalse is null
            ? null
            : new TemplateConditionalSyntaxNode(
                conditional.Condition,
                whenTrue,
                whenFalse);
    }

    private static TemplateMemberValueSyntaxNode?
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

            TemplateMemberValueSyntaxNode? result;

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
                            TemplateMemberValueConditionalSyntaxNode(
                                SyntaxFactory.IdentifierName(
                                    conditionPlaceholder),
                                whenTrue,
                                whenFalse);
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
                return new TemplateMemberValueLeafSyntaxNode(
                    expression,
                    IsDsl: false);
            }

            return new TemplateMemberValueConditionalSyntaxNode(
                conditionalExpression.Condition,
                whenTrue,
                whenFalse);
        }

        var isDsl = IsMemberDslExpression(
            unwrapped,
            semanticModel,
            cancellationToken);

        return new TemplateMemberValueLeafSyntaxNode(
            expression,
            isDsl);
    }

    private static bool ContainsDslMemberValue(
        TemplateMemberValueSyntaxNode node)
    {
        return node switch
        {
            TemplateMemberValueLeafSyntaxNode leaf =>
                leaf.IsDsl,
            TemplateMemberValueConditionalSyntaxNode =>
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
        out ImmutableArray<TemplateMemberAssignmentSyntax>
            assignments)
    {
        var result =
            ImmutableArray.CreateBuilder<
                TemplateMemberAssignmentSyntax>();
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
                new TemplateMemberAssignmentSyntax(
                    memberName.Identifier.ValueText,
                    assignment.Right));
        }

        assignments = result.ToImmutable();
        return true;
    }

    private static ImmutableArray<TemplateObjectArgumentSyntax>
        BuildObjectArguments(
            BaseObjectCreationExpressionSyntax objectCreation)
    {
        return (objectCreation.ArgumentList?.Arguments ??
                default)
            .Select(argument =>
            {
                ImmutableArray<TemplateMemberAssignmentSyntax>?
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

                return new TemplateObjectArgumentSyntax(
                    argument,
                    argument.Expression,
                    memberAssignments);
            })
            .ToImmutableArray();
    }

    private static bool IsTemplateObjectCreation(
        BaseObjectCreationExpressionSyntax objectCreation,
        ITypeSymbol? templateResultType,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (templateResultType is null)
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
                   templateResultType);
    }

    private static bool IsDslLocalType(
        ITypeSymbol type,
        ITypeSymbol? templateResultType)
    {
        if (templateResultType is
                {
                    TypeKind: not TypeKind.Error
                } &&
            type.TypeKind != TypeKind.Error &&
            SymbolEqualityComparer.Default.Equals(
                type,
                templateResultType))
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
            ConstructorMemberMetadataName)
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
        ITypeSymbol? templateResultType,
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
                       templateResultType,
                       dslLocals,
                       semanticModel,
                       cancellationToken) ||
                   ContainsDslLocalInitializer(
                       conditional.WhenFalse,
                       templateResultType,
                       dslLocals,
                       semanticModel,
                       cancellationToken);
        }

        if (expression is WithExpressionSyntax withExpression)
        {
            return ContainsDslLocalInitializer(
                withExpression.Expression,
                templateResultType,
                dslLocals,
                semanticModel,
                cancellationToken);
        }

        if (templateResultType is not null &&
            expression is BaseObjectCreationExpressionSyntax
                objectCreation &&
            IsTemplateObjectCreation(
                objectCreation,
                templateResultType,
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
        foreach (var expression in expressions)
        {
            foreach (var identifier in expression
                         .DescendantNodesAndSelf()
                         .OfType<IdentifierNameSyntax>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var symbol = semanticModel.GetSymbolInfo(
                        identifier,
                        cancellationToken)
                    .Symbol;

                if (symbol is ILocalSymbol &&
                    !allowedLocals.Contains(symbol) ||
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

    private static string AllocatePlaceholder(
        ref int ordinal,
        HashSet<string> reservedNames)
    {
        while (true)
        {
            var candidate =
                "__morphantTemplateLocal" +
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
}

internal sealed record TemplateControlFlowProgram(
    TemplateControlFlowSyntaxNode Root,
    ImmutableArray<TemplateRuntimeLocalSyntax> RuntimeLocals,
    IReadOnlyDictionary<ISymbol, string> RuntimeLocalPlaceholders);

internal readonly record struct TemplateRuntimeLocalSyntax(
    string PlaceholderName,
    string PreferredName,
    string DeclarationType,
    ExpressionSyntax Initializer);

internal abstract record TemplateControlFlowSyntaxNode;

internal sealed record TemplateConditionalSyntaxNode(
    ExpressionSyntax Condition,
    TemplateControlFlowSyntaxNode WhenTrue,
    TemplateControlFlowSyntaxNode WhenFalse)
    : TemplateControlFlowSyntaxNode;

internal sealed record TemplateLeafSyntaxNode(
    ExpressionSyntax? DirectExpression,
    BaseObjectCreationExpressionSyntax? ObjectCreation,
    ImmutableArray<TemplateObjectArgumentSyntax> Arguments,
    ImmutableArray<TemplateMemberAssignmentSyntax> MemberAssignments)
    : TemplateControlFlowSyntaxNode;

internal readonly record struct TemplateObjectArgumentSyntax(
    ArgumentSyntax Syntax,
    ExpressionSyntax Value,
    ImmutableArray<TemplateMemberAssignmentSyntax>? MemberAssignments);

internal readonly record struct TemplateMemberAssignmentSyntax(
    string MemberName,
    ExpressionSyntax Value);

internal abstract record TemplateMemberValueSyntaxNode;

internal sealed record TemplateMemberValueConditionalSyntaxNode(
    ExpressionSyntax Condition,
    TemplateMemberValueSyntaxNode WhenTrue,
    TemplateMemberValueSyntaxNode WhenFalse)
    : TemplateMemberValueSyntaxNode;

internal sealed record TemplateMemberValueLeafSyntaxNode(
    ExpressionSyntax Value,
    bool IsDsl)
    : TemplateMemberValueSyntaxNode;
