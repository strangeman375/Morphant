using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

// Facts about the public operation and the input destination. They never imply
// that a replacement selected by Resolve has already been constructed.
internal sealed class DeclarativeExecutionFacts(
    SemanticModel semanticModel,
    IParameterSymbol? previousParameter,
    bool? hasPrevious,
    IParameterSymbol? contextParameter,
    MappingExecutionPathSet? executionPath,
    IReadOnlyDictionary<ISymbol, ExpressionSyntax>? localInitializers = null,
    CancellationToken cancellationToken = default)
{
    public bool TryBoolean(ExpressionSyntax expression, out bool value)
    {
        if (TryConstant(expression, new HashSet<ISymbol>(SymbolEqualityComparer.Default), out var constant) &&
            constant is bool boolean)
        {
            value = boolean;
            return true;
        }
        value = false;
        return false;
    }

    public bool TryPattern(ExpressionSyntax expression, PatternSyntax pattern, out bool value)
    {
        if (TryConstant(expression, new HashSet<ISymbol>(SymbolEqualityComparer.Default), out var constant))
            return TryPattern(constant, pattern, new HashSet<ISymbol>(SymbolEqualityComparer.Default), out value);
        value = false;
        return false;
    }

    public static bool? MatchConstantPattern(
        object? input, PatternSyntax pattern, SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var facts = new DeclarativeExecutionFacts(semanticModel, null, null, null,
            null, cancellationToken: cancellationToken);
        return facts.TryPattern(input, pattern,
            new HashSet<ISymbol>(SymbolEqualityComparer.Default), out var matches)
            ? matches : null;
    }

    public bool References(ExpressionSyntax expression, IParameterSymbol parameter)
    {
        var visiting = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        bool Visit(SyntaxNode node)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (node is InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } } invocation &&
                invocation.SyntaxTree == semanticModel.SyntaxTree &&
                semanticModel.GetConstantValue(invocation).HasValue)
                return false;
            if (node is ConditionalExpressionSyntax conditional && TryBoolean(conditional.Condition, out var selected))
                return Visit(selected ? conditional.WhenTrue : conditional.WhenFalse);
            if (node is SwitchExpressionSyntax @switch && SelectArm(@switch) is { } arm)
                return Visit(arm.Expression);
            if (node is BinaryExpressionSyntax binary &&
                binary.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression &&
                TryBoolean(binary.Left, out var left))
                return (binary.IsKind(SyntaxKind.LogicalAndExpression) ? left : !left) && Visit(binary.Right);
            if (node is IdentifierNameSyntax identifier && identifier.SyntaxTree == semanticModel.SyntaxTree)
            {
                var symbol = semanticModel.GetSymbolInfo(identifier).Symbol;
                if (SymbolEqualityComparer.Default.Equals(symbol, parameter)) return true;
                if (symbol is not null && localInitializers is not null &&
                    localInitializers.TryGetValue(symbol, out var initializer) && visiting.Add(symbol))
                {
                    var referenced = Visit(initializer);
                    visiting.Remove(symbol);
                    return referenced;
                }
            }
            return node.ChildNodes().Any(Visit);
        }
        return Visit(expression);
    }

    public SwitchExpressionArmSyntax? SelectArm(SwitchExpressionSyntax expression)
    {
        foreach (var arm in expression.Arms)
        {
            if (!TryPattern(expression.GoverningExpression, arm.Pattern, out var matches)) return null;
            if (!matches) continue;
            if (arm.WhenClause is { } guard)
            {
                if (!TryBoolean(guard.Condition, out var enabled)) return null;
                if (!enabled) continue;
            }
            return arm;
        }
        return null;
    }

    public DeclarativeControlFlowSyntaxNode Prune(DeclarativeControlFlowSyntaxNode node) => node switch
    {
        DeclarativeConditionalSyntaxNode conditional when TryBoolean(conditional.Condition, out var selected) =>
            Prune(selected ? conditional.WhenTrue : conditional.WhenFalse),
        DeclarativeConditionalSyntaxNode conditional => conditional with
        {
            WhenTrue = Prune(conditional.WhenTrue), WhenFalse = Prune(conditional.WhenFalse)
        },
        DeclarativeLocalDeclarationsSyntaxNode locals => locals with { Next = Prune(locals.Next) },
        DeclarativeEvaluationSyntaxNode evaluation => evaluation with { Next = Prune(evaluation.Next) },
        DeclarativeSwitchSyntaxNode @switch => PruneSwitch(@switch),
        _ => node
    };

    private DeclarativeControlFlowSyntaxNode PruneSwitch(DeclarativeSwitchSyntaxNode node)
    {
        if (TryConstant(node.GoverningExpression, new HashSet<ISymbol>(SymbolEqualityComparer.Default), out var input))
        {
            DeclarativeControlFlowSyntaxNode? fallback = node.Continuation;
            foreach (var section in node.Sections)
            {
                foreach (var label in section.Labels)
                {
                    if (label.Kind == DeclarativeSwitchLabelKind.Default)
                    {
                        fallback = section.Branch;
                        continue;
                    }
                    bool matches;
                    var visiting = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
                    if (label.Kind == DeclarativeSwitchLabelKind.Value && label.Value is { } value &&
                        TryConstant(value, visiting, out var expected))
                        matches = Equals(input, expected);
                    else if (label.Pattern is { } pattern && TryPattern(input, pattern, visiting, out matches))
                    { }
                    else
                        return PruneBranches();
                    if (!matches) continue;
                    if (label.WhenCondition is { } condition)
                    {
                        if (!TryBoolean(condition, out var enabled)) return PruneBranches();
                        if (!enabled) continue;
                    }
                    return Prune(section.Branch);
                }
            }
            if (fallback is not null) return Prune(fallback);
        }
        return PruneBranches();

        DeclarativeSwitchSyntaxNode PruneBranches() => node with
        {
            Sections = node.Sections.Select(section => section with { Branch = Prune(section.Branch) }).ToImmutableArray(),
            Continuation = node.Continuation is { } continuation ? Prune(continuation) : null
        };
    }

    private bool TryConstant(ExpressionSyntax expression, HashSet<ISymbol> visiting, out object? value)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (expression is ParenthesizedExpressionSyntax parentheses)
            return TryConstant(parentheses.Expression, visiting, out value);
        if (expression.SyntaxTree == semanticModel.SyntaxTree)
        {
            var constant = semanticModel.GetConstantValue(expression);
            if (constant.HasValue) { value = constant.Value; return true; }
        }
        if (expression is IdentifierNameSyntax identifier && identifier.SyntaxTree == semanticModel.SyntaxTree &&
            semanticModel.GetSymbolInfo(identifier).Symbol is ILocalSymbol symbol &&
            (symbol.Type.SpecialType == SpecialType.System_Boolean || symbol.Type.TypeKind == TypeKind.Enum) &&
            localInitializers is not null && localInitializers.TryGetValue(symbol, out var initializer) && visiting.Add(symbol))
        {
            // Runtime numeric equality involves promotions and NaN semantics.
            // Only boolean/enum aliases can establish an operation fact here.
            var known = TryConstant(initializer, visiting, out value);
            visiting.Remove(symbol);
            return known;
        }
        if (expression is MemberAccessExpressionSyntax member && member.Expression.SyntaxTree == semanticModel.SyntaxTree)
        {
            var receiver = semanticModel.GetSymbolInfo(member.Expression).Symbol;
            if (previousParameter is not null && hasPrevious is { } available &&
                member.Name.Identifier.ValueText == "HasValue" && SymbolEqualityComparer.Default.Equals(receiver, previousParameter))
            { value = available; return true; }
            if (contextParameter is not null && executionPath is { } path && path != MappingExecutionPathSet.NoPrevious &&
                member.Name.Identifier.ValueText == "Operation" && SymbolEqualityComparer.Default.Equals(receiver, contextParameter))
            { value = path == MappingExecutionPathSet.Create ? 1 : 2; return true; }
        }
        if (expression is PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalNotExpression } unary &&
            TryConstant(unary.Operand, visiting, out var operand) && operand is bool operandBoolean)
        { value = !operandBoolean; return true; }
        if (expression is BinaryExpressionSyntax binary && TryConstant(binary.Left, visiting, out var left))
        {
            if (binary.IsKind(SyntaxKind.LogicalAndExpression) && left is false) { value = false; return true; }
            if (binary.IsKind(SyntaxKind.LogicalOrExpression) && left is true) { value = true; return true; }
            if (TryConstant(binary.Right, visiting, out var right))
            {
                switch (binary.Kind())
                {
                    case SyntaxKind.EqualsExpression: value = Equals(left, right); return true;
                    case SyntaxKind.IsExpression: value = Equals(left, right); return true;
                    case SyntaxKind.NotEqualsExpression: value = !Equals(left, right); return true;
                    case SyntaxKind.LogicalAndExpression when left is bool l && right is bool r: value = l && r; return true;
                    case SyntaxKind.LogicalOrExpression when left is bool l && right is bool r: value = l || r; return true;
                }
            }
        }
        if (expression is IsPatternExpressionSyntax patternExpression &&
            TryConstant(patternExpression.Expression, visiting, out var input) &&
            TryPattern(input, patternExpression.Pattern, visiting, out var matches))
        { value = matches; return true; }
        value = null;
        return false;
    }

    private bool TryPattern(object? input, PatternSyntax pattern, HashSet<ISymbol> visiting, out bool value)
    {
        switch (pattern)
        {
            case ConstantPatternSyntax constant when TryConstant(constant.Expression, visiting, out var expected):
                value = Equals(input, expected); return true;
            case DiscardPatternSyntax: value = true; return true;
            case ParenthesizedPatternSyntax parentheses: return TryPattern(input, parentheses.Pattern, visiting, out value);
            case UnaryPatternSyntax unary when unary.IsKind(SyntaxKind.NotPattern) && TryPattern(input, unary.Pattern, visiting, out var inner):
                value = !inner; return true;
            case BinaryPatternSyntax binary when TryPattern(input, binary.Left, visiting, out var left):
                if (binary.IsKind(SyntaxKind.AndPattern) && !left) { value = false; return true; }
                if (binary.IsKind(SyntaxKind.OrPattern) && left) { value = true; return true; }
                if (TryPattern(input, binary.Right, visiting, out var right))
                { value = binary.IsKind(SyntaxKind.AndPattern) ? left && right : left || right; return true; }
                break;
        }
        value = false;
        return false;
    }
}
