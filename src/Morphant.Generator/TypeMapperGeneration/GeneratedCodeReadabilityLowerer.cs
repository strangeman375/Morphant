using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class GeneratedCodeReadabilityLowerer
{
    public static TypeMapperModel Lower(TypeMapperModel model)
    {
        return model with
        {
            Mappings = model.Mappings
                .Select(LowerMapping)
                .ToImmutableArray()
        };
    }

    private static TypeMapperMappingModel LowerMapping(
        TypeMapperMappingModel mapping)
    {
        var createNames = TypeMapperMethodLocalNames.Build(
            mapping,
            create: true);
        var updateNames = TypeMapperMethodLocalNames.Build(
            mapping,
            create: false);

        if (mapping.ControlFlow is { } controlFlow)
        {
            return mapping with
            {
                ControlFlow = new TypeMapperControlFlowMappingModel(
                    LowerNode(
                        controlFlow.CreateRoot,
                        create: true,
                        createNames),
                    LowerNode(
                        controlFlow.UpdateRoot,
                        create: false,
                        updateNames))
            };
        }

        var lowered = LowerCreateLeaf(mapping, createNames);
        return LowerUpdateLeaf(
            lowered,
            allowReplacement: false,
            updateNames);
    }

    private static TypeMapperControlFlowNode LowerNode(
        TypeMapperControlFlowNode node,
        bool create,
        GeneratedLocalNameAllocator names)
    {
        if (node.EvaluationContinuation is
                { } evaluationContinuation)
        {
            var continuation = LowerNode(
                evaluationContinuation,
                create,
                names);

            if (TryLowerGuardedEvaluation(
                    node.EvaluationExpression!,
                    names.Clone(),
                    out var condition,
                    out var expression,
                    out var locals))
            {
                return node with
                {
                    EvaluationExpression = expression,
                    EvaluationContinuation = continuation,
                    EvaluationCondition = condition,
                    EvaluationLocals = locals
                };
            }

            return node with
            {
                EvaluationContinuation = continuation
            };
        }

        if (node.SwitchExpression is not null)
        {
            var continuation = node.SwitchContinuation is
                    { } switchContinuation
                ? LowerNode(
                    switchContinuation,
                    create,
                    names)
                : null;

            return node with
            {
                SwitchSections = Normalize(node.SwitchSections)
                    .Select(section => section with
                    {
                        Branch = LowerNode(
                            section.Branch,
                            create,
                            names.Clone())
                    })
                    .ToImmutableArray(),
                SwitchContinuation = continuation
            };
        }

        if (node.Condition is not null)
        {
            return node with
            {
                WhenTrue = LowerNode(
                    node.WhenTrue!,
                    create,
                    names.Clone()),
                WhenFalse = LowerNode(
                    node.WhenFalse!,
                    create,
                    names.Clone())
            };
        }

        if (node.Leaf is not { } leaf)
        {
            return node;
        }

        return node with
        {
            Leaf = create
                ? LowerCreateLeaf(leaf, names)
                : LowerUpdateLeaf(
                    leaf,
                    allowReplacement: true,
                    names)
        };
    }

    private static TypeMapperMappingModel LowerCreateLeaf(
        TypeMapperMappingModel mapping,
        GeneratedLocalNameAllocator names)
    {
        var constructor = mapping.CreateConstructor is
                { } createConstructor
            ? createConstructor with
            {
                Arguments = createConstructor.Arguments
                    .Select(argument => LowerArgument(
                        argument,
                        names))
                    .ToImmutableArray()
            }
            : (TypeMapperConstructorMappingModel?)null;

        return mapping with
        {
            CreateConstructor = constructor,
            CreatePostMemberMappings = LowerMembers(
                mapping.CreatePostMemberMappings,
                names),
            PostMemberControlFlow = mapping.PostMemberControlFlow is
                    { } postMemberControlFlow
                ? LowerMemberNode(postMemberControlFlow, names)
                : null
        };
    }

    private static TypeMapperMappingModel LowerUpdateLeaf(
        TypeMapperMappingModel mapping,
        bool allowReplacement,
        GeneratedLocalNameAllocator names)
    {
        if (allowReplacement &&
            (mapping.CreateFactory is not null ||
             mapping.CreateConstructor is not null))
        {
            return LowerCreateLeaf(mapping, names);
        }

        return mapping with
        {
            UpdateMemberMappings = LowerMembers(
                mapping.UpdateMemberMappings,
                names),
            PostMemberControlFlow = mapping.PostMemberControlFlow is
                    { } postMemberControlFlow
                ? LowerMemberNode(postMemberControlFlow, names)
                : null
        };
    }

    private static TypeMapperMemberControlFlowNode LowerMemberNode(
        TypeMapperMemberControlFlowNode node,
        GeneratedLocalNameAllocator names)
    {
        if (node.EvaluationContinuation is
                { } evaluationContinuation)
        {
            var continuation = LowerMemberNode(
                evaluationContinuation,
                names);

            if (TryLowerGuardedEvaluation(
                    node.EvaluationExpression!,
                    names.Clone(),
                    out var condition,
                    out var expression,
                    out var locals))
            {
                return node with
                {
                    EvaluationExpression = expression,
                    EvaluationContinuation = continuation,
                    EvaluationCondition = condition,
                    EvaluationLocals = locals
                };
            }

            return node with
            {
                EvaluationContinuation = continuation
            };
        }

        if (node.SwitchExpression is not null)
        {
            var continuation = node.SwitchContinuation is
                    { } switchContinuation
                ? LowerMemberNode(switchContinuation, names)
                : null;

            return node with
            {
                SwitchSections = Normalize(node.SwitchSections)
                    .Select(section => section with
                    {
                        Branch = LowerMemberNode(
                            section.Branch,
                            names.Clone())
                    })
                    .ToImmutableArray(),
                SwitchContinuation = continuation
            };
        }

        if (node.Condition is not null)
        {
            return node with
            {
                WhenTrue = LowerMemberNode(
                    node.WhenTrue!,
                    names.Clone()),
                WhenFalse = LowerMemberNode(
                    node.WhenFalse!,
                    names.Clone())
            };
        }

        return node with
        {
            MemberMappings = LowerMembers(
                node.MemberMappings,
                names)
        };
    }

    private static ImmutableArray<TypeMapperMemberMappingModel>
        LowerMembers(
        ImmutableArray<TypeMapperMemberMappingModel> mappings,
        GeneratedLocalNameAllocator names)
    {
        return Normalize(mappings)
            .Select(mapping =>
            {
                if (mapping.ExplicitValueExpression is not
                        { } expression ||
                    !TryLowerMapInvocation(
                        expression,
                        mapping.DestinationMemberName,
                        forceSourceLocal: false,
                        names,
                        out var lowered))
                {
                    return mapping;
                }

                return mapping with
                {
                    ExplicitValueExpression = lowered.Expression,
                    EvaluationLocals = mapping.ValueLocalName is not null
                        ? Normalize(mapping.EvaluationLocals)
                            .AddRange(lowered.Locals)
                        : mapping.EvaluationLocals,
                    InvocationArgumentLocals =
                        mapping.ValueLocalName is null
                            ? lowered.Locals
                            : mapping.InvocationArgumentLocals
                };
            })
            .ToImmutableArray();
    }

    private static TypeMapperConstructorArgumentMappingModel LowerArgument(
        TypeMapperConstructorArgumentMappingModel argument,
        GeneratedLocalNameAllocator names)
    {
        if (argument.ExplicitValueExpression is not
                { } expression ||
            !TryLowerMapInvocation(
                expression,
                argument.ParameterName,
                forceSourceLocal: false,
                names,
                out var lowered))
        {
            return argument;
        }

        return argument with
        {
            ExplicitValueExpression = lowered.Expression,
            EvaluationLocals = Normalize(argument.EvaluationLocals)
                .AddRange(lowered.Locals)
        };
    }

    private static bool TryLowerGuardedEvaluation(
        string expression,
        GeneratedLocalNameAllocator names,
        out string condition,
        out string loweredExpression,
        out ImmutableArray<TypeMapperLocalValueModel> locals)
    {
        var syntax = UnwrapParentheses(
            SyntaxFactory.ParseExpression(expression));

        if (syntax is not ConditionalExpressionSyntax conditional ||
            !IsDiscardedDefault(conditional.WhenFalse) ||
            !TryLowerMapInvocation(
                Normalize(conditional.WhenTrue),
                "Nested",
                forceSourceLocal: true,
                names,
                out var lowered))
        {
            condition = string.Empty;
            loweredExpression = string.Empty;
            locals = ImmutableArray<TypeMapperLocalValueModel>.Empty;
            return false;
        }

        condition = Normalize(conditional.Condition);
        loweredExpression = lowered.Expression;
        locals = lowered.Locals;
        return true;
    }

    private static bool TryLowerMapInvocation(
        string expression,
        string targetName,
        bool forceSourceLocal,
        GeneratedLocalNameAllocator names,
        out LoweredInvocation lowered)
    {
        var syntax = UnwrapParentheses(
            SyntaxFactory.ParseExpression(expression));

        if (syntax is not InvocationExpressionSyntax invocation ||
            !IsGeneratedMapInvocation(invocation))
        {
            lowered = default;
            return false;
        }

        var arguments = invocation.ArgumentList.Arguments;
        var destinationIndex = -1;

        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index].NameColon?.Name.Identifier.ValueText ==
                "destination")
            {
                destinationIndex = index;
                break;
            }
        }

        var hasComplexDestination = destinationIndex >= 0 &&
            UnwrapParentheses(arguments[destinationIndex].Expression) is
                SwitchExpressionSyntax or ConditionalExpressionSyntax;

        if (!forceSourceLocal && !hasComplexDestination)
        {
            lowered = default;
            return false;
        }

        var locals = ImmutableArray
            .CreateBuilder<TypeMapperLocalValueModel>();
        var rewrittenArguments = arguments;
        var sourceIndex = FindSourceArgument(arguments);

        if (sourceIndex >= 0 &&
            (forceSourceLocal || hasComplexDestination) &&
            !IsStableValue(arguments[sourceIndex].Expression))
        {
            var sourceName = names.Allocate(
                BuildSourceLocalName(
                    arguments[sourceIndex].Expression,
                    invocation));
            locals.Add(new TypeMapperLocalValueModel(
                "var",
                sourceName,
                Normalize(arguments[sourceIndex].Expression),
                IsConst: false,
                IsSynthetic: true));
            rewrittenArguments = rewrittenArguments.Replace(
                rewrittenArguments[sourceIndex],
                rewrittenArguments[sourceIndex].WithExpression(
                    SyntaxFactory.IdentifierName(sourceName)));
        }

        if (hasComplexDestination)
        {
            var destinationName = names.Allocate(
                BuildDestinationLocalName(targetName));
            var destinationExpression = UnwrapParentheses(
                arguments[destinationIndex].Expression);
            locals.Add(new TypeMapperLocalValueModel(
                "var",
                destinationName,
                FormatComplexExpression(destinationExpression),
                IsConst: false,
                IsSynthetic: true));
            rewrittenArguments = rewrittenArguments.Replace(
                rewrittenArguments[destinationIndex],
                rewrittenArguments[destinationIndex].WithExpression(
                    SyntaxFactory.IdentifierName(destinationName)));
        }

        var rewritten = invocation.WithArgumentList(
            invocation.ArgumentList.WithArguments(rewrittenArguments));
        lowered = new LoweredInvocation(
            FormatInvocation(rewritten),
            locals.ToImmutable());
        return true;
    }

    private static int FindSourceArgument(
        SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            var name = arguments[index].NameColon?.Name.Identifier.ValueText;

            if (name is null or "source")
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsGeneratedMapInvocation(
        InvocationExpressionSyntax invocation)
    {
        return invocation.Expression is MemberAccessExpressionSyntax
        {
            Name: GenericNameSyntax
            {
                Identifier.ValueText: "Map",
                TypeArgumentList.Arguments.Count: 2
            },
            Expression: MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "Mapper"
            }
        };
    }

    private static bool IsDiscardedDefault(ExpressionSyntax expression)
    {
        expression = UnwrapParentheses(expression);
        return expression is DefaultExpressionSyntax or
            LiteralExpressionSyntax
            {
                RawKind: (int)SyntaxKind.DefaultLiteralExpression
            };
    }

    private static bool IsStableValue(ExpressionSyntax expression)
    {
        expression = UnwrapParentheses(expression);

        if (expression is IdentifierNameSyntax or LiteralExpressionSyntax or
            DefaultExpressionSyntax)
        {
            return true;
        }

        return expression is PostfixUnaryExpressionSyntax
            {
                RawKind:
                    (int)SyntaxKind.SuppressNullableWarningExpression,
                Operand: var operand
            } && IsStableValue(operand);
    }

    private static string BuildSourceLocalName(
        ExpressionSyntax source,
        InvocationExpressionSyntax invocation)
    {
        source = UnwrapParentheses(source);

        if (source is MemberAccessExpressionSyntax member)
        {
            return ToCamelCase(member.Name.Identifier.ValueText) +
                   "Source";
        }

        if (invocation.Expression is MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax genericName
            } &&
            genericName.TypeArgumentList.Arguments[0] is { } sourceType)
        {
            var typeName = sourceType
                .DescendantNodesAndSelf()
                .OfType<SimpleNameSyntax>()
                .LastOrDefault()
                ?.Identifier.ValueText;

            if (!string.IsNullOrEmpty(typeName) &&
                !StringComparer.Ordinal.Equals(typeName, "Int32"))
            {
                return ToCamelCase(typeName!) switch
                {
                    "int" => "nestedSource",
                    var name when name.EndsWith(
                        "Source",
                        StringComparison.Ordinal) => name,
                    var name => name + "Source"
                };
            }
        }

        return "nestedSource";
    }

    private static string BuildDestinationLocalName(string targetName)
    {
        var name = ToCamelCase(targetName);
        return name.EndsWith(
                "Destination",
                StringComparison.Ordinal)
            ? name
            : name + "Destination";
    }

    private static string ToCamelCase(string value)
    {
        return string.IsNullOrEmpty(value)
            ? "value"
            : char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static string FormatInvocation(
        InvocationExpressionSyntax invocation)
    {
        var lines = new List<string>
        {
            Normalize(invocation.Expression) + "("
        };
        var arguments = invocation.ArgumentList.Arguments;

        for (var index = 0; index < arguments.Count; index++)
        {
            lines.Add(
                "    " + Normalize(arguments[index]) +
                (index == arguments.Count - 1 ? ")" : ","));
        }

        return string.Join("\n", lines);
    }

    private static string FormatComplexExpression(
        ExpressionSyntax expression)
    {
        return expression is SwitchExpressionSyntax switchExpression
            ? FormatSwitchExpression(switchExpression)
            : Normalize(expression);
    }

    private static string FormatSwitchExpression(
        SwitchExpressionSyntax expression)
    {
        var lines = new List<string>
        {
            Normalize(expression.GoverningExpression) + " switch",
            "{"
        };

        foreach (var arm in expression.Arms)
        {
            var pattern = Normalize(arm.Pattern) +
                (arm.WhenClause is { } whenClause
                    ? " " + Normalize(whenClause)
                    : string.Empty);
            var armValue = FormatSwitchArmValue(arm.Expression);
            lines.Add("    " + pattern + " => " + armValue[0]);

            for (var index = 1; index < armValue.Count; index++)
            {
                lines.Add("        " + armValue[index]);
            }

            lines[lines.Count - 1] += ",";
        }

        lines.Add("}");
        return string.Join("\n", lines);
    }

    private static IReadOnlyList<string> FormatSwitchArmValue(
        ExpressionSyntax expression)
    {
        if (expression is ThrowExpressionSyntax
            {
                Expression: InvocationExpressionSyntax invocation
            } &&
            TryFormatNestedDestinationMismatch(
                invocation,
                out var invocationLines))
        {
            return new[] { "throw" }
                .Concat(invocationLines)
                .ToArray();
        }

        return new[] { Normalize(expression) };
    }

    private static bool TryFormatNestedDestinationMismatch(
        InvocationExpressionSyntax invocation,
        out IReadOnlyList<string> lines)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax
            {
                Name: GenericNameSyntax
                {
                    Identifier.ValueText: "Create"
                } createMethod
            } ||
            !Normalize(invocation.Expression).StartsWith(
                "global::Morphant.Exceptions." +
                "NestedDestinationTypeMismatchException.Create<",
                StringComparison.Ordinal))
        {
            lines = Array.Empty<string>();
            return false;
        }

        var result = new List<string>
        {
            "global::Morphant.Exceptions",
            "    .NestedDestinationTypeMismatchException"
        };
        var typeArguments = createMethod.TypeArgumentList.Arguments;
        var compactGenericCall = "    .Create<" +
            string.Join(
                ", ",
                typeArguments.Select(Normalize)) +
            ">(";

        if (compactGenericCall.Length <= 72)
        {
            result.Add(compactGenericCall);
        }
        else
        {
            result.Add("    .Create<");

            for (var index = 0; index < typeArguments.Count; index++)
            {
                result.Add(
                    "        " + Normalize(typeArguments[index]) +
                    (index == typeArguments.Count - 1 ? ">(" : ","));
            }
        }

        var arguments = invocation.ArgumentList.Arguments;

        for (var index = 0; index < arguments.Count; index++)
        {
            result.Add(
                "        " + Normalize(arguments[index]) +
                (index == arguments.Count - 1 ? ")" : ","));
        }

        lines = result;
        return true;
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

    private static string Normalize(SyntaxNode syntax)
    {
        return syntax.WithoutTrivia()
            .NormalizeWhitespace()
            .ToFullString()
            .Replace("? )", "?)");
    }

    private static ImmutableArray<T> Normalize<T>(
        ImmutableArray<T> values)
    {
        return values.IsDefault
            ? ImmutableArray<T>.Empty
            : values;
    }

    private readonly record struct LoweredInvocation(
        string Expression,
        ImmutableArray<TypeMapperLocalValueModel> Locals);
}
