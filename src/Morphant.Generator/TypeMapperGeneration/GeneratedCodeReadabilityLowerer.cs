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
        mapping = CollapseTupleConstruction(mapping, names);
        if (mapping.CreateConstructor is { TupleConstruction: null } &&
            mapping.CreatePostMemberMappings.Any(static member => member.IsResultDependent) &&
            !mapping.CreateMemberMappings.IsEmpty &&
            mapping.PostMemberControlFlow is null && mapping.MemberObservation is { } observation &&
            mapping.CreateMemberMappings.All(member => !member.IsRequired &&
                observation.Rules.Any(rule =>
                    StringComparer.Ordinal.Equals(rule.DestinationMember.Name, member.DestinationMemberName) &&
                    rule.Lifecycle.HasFlag(MemberLifecycleDependency.ExistingDestination))))
        {
            // A source-only initializer must not overwrite a value that a
            // neighboring member expression reads from the initial result.
            mapping = mapping with
            {
                CreatePostMemberMappings = mapping.CreateMemberMappings.AddRange(mapping.CreatePostMemberMappings)
                    .OrderBy(member => observation.Rules.TakeWhile(rule =>
                        !StringComparer.Ordinal.Equals(rule.DestinationMember.Name, member.DestinationMemberName)).Count())
                    .ToImmutableArray(),
                CreateMemberMappings = ImmutableArray<TypeMapperMemberMappingModel>.Empty
            };
        }

        var constructor = mapping.CreateConstructor is
                { } createConstructor
            ? createConstructor with
            {
                Arguments = LowerArguments(createConstructor.Arguments, mapping.NonNullSourceName, names)
            }
            : (TypeMapperConstructorMappingModel?)null;

        return mapping with
        {
            CreateConstructor = constructor,
            CreateMemberMappings = mapping.CreateMemberMappings
                .Select(FormatMemberExpression).ToImmutableArray(),
            CreatePostMemberMappings = LowerMembers(
                mapping.CreatePostMemberMappings,
                mapping.NonNullSourceName,
                names),
            PostMemberControlFlow = mapping.PostMemberControlFlow is
                    { } postMemberControlFlow
                ? LowerMemberNode(postMemberControlFlow, mapping.NonNullSourceName, names)
                : null
        };
    }

    private static string AllocateValueLocalName(GeneratedLocalNameAllocator names, string memberName)
    {
        var name = names.AllocateForSourcePathSegment(memberName);
        return SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;
    }

    private static TypeMapperMappingModel CollapseTupleConstruction(
        TypeMapperMappingModel mapping,
        GeneratedLocalNameAllocator names)
    {
        if (mapping.CreateConstructor is not { TupleConstruction: not null } constructor ||
            mapping.PostMemberControlFlow is null && mapping.CreatePostMemberMappings.IsEmpty ||
            mapping.CreatePostMemberMappings.Any(member => member.IsResultDependent))
        {
            return mapping;
        }

        if (mapping.PostMemberControlFlow is { } controlFlow)
        {
            if (ReferencesResult(controlFlow, mapping.ResultLocalName)) return mapping;
            var initialArguments = constructor.Arguments.Select(argument => argument with
            {
                ValueLocalName = argument.ValueLocalName ?? AllocateValueLocalName(names, argument.ParameterName),
                ValueLocalTypeName = argument.ValueLocalTypeName ?? argument.TargetTypeName
            }).ToImmutableArray();
            return mapping with
            {
                CreateConstructor = constructor with { Arguments = initialArguments, DeferTupleConstruction = true },
                CreateTupleReconstruction = new TypeMapperTupleReconstructionModel(
                    constructor.TupleConstruction.Value,
                    initialArguments.OrderBy(argument => argument.TupleElementOrdinal).Select(argument =>
                        new TypeMapperTupleElementModel(argument.ParameterName, argument.ParameterName, argument.ValueLocalName))
                        .ToImmutableArray())
            };
        }

        var arguments = constructor.Arguments.Select(argument => argument with
        {
            ValueLocalName = argument.ValueLocalName ?? AllocateValueLocalName(names, argument.ParameterName),
            ValueLocalTypeName = argument.ValueLocalTypeName ?? argument.TargetTypeName,
            IsEvaluationOnly = mapping.CreatePostMemberMappings.Any(member =>
                StringComparer.Ordinal.Equals(member.DestinationMemberName, argument.ParameterName))
        }).ToImmutableArray();
        var finalArguments = mapping.CreatePostMemberMappings.Select(member =>
        {
            var initial = arguments.Single(argument =>
                StringComparer.Ordinal.Equals(argument.ParameterName, member.DestinationMemberName));
            return initial with
            {
                SourceMemberName = member.SourceMemberName,
                ExplicitValueExpression = member.SourceValueLocalName ?? member.ExplicitValueExpression,
                ConventionValueExpression = member.ConventionValueExpression,
                DependencyExpression = member.DependencyExpression,
                EvaluationLocals = Normalize(member.EvaluationLocals).AddRange(Normalize(member.InvocationArgumentLocals)),
                RuleOriginNode = null,
                SourceMemberSymbol = null,
                IsEvaluationOnly = false
            };
        });
        return mapping with
        {
            CreateConstructor = constructor with { Arguments = arguments.AddRange(finalArguments) },
            CreatePostMemberMappings = ImmutableArray<TypeMapperMemberMappingModel>.Empty,
            CreateTupleReconstruction = null
        };
    }

    private static bool ReferencesResult(TypeMapperMemberControlFlowNode node, string resultName)
    {
        bool References(string? expression) => expression is not null &&
            SyntaxFactory.ParseExpression(expression).DescendantNodesAndSelf().OfType<IdentifierNameSyntax>()
                .Any(identifier => identifier.Identifier.ValueText == resultName &&
                    !(identifier.Parent is MemberAccessExpressionSyntax access && access.Name == identifier));
        return References(node.Condition) || References(node.SwitchExpression) || References(node.EvaluationExpression) ||
            References(node.ThrowExpression) || node.Locals.Any(local => References(local.ValueExpression)) ||
            node.MemberMappings.Any(member => member.IsResultDependent || References(member.ExplicitValueExpression)) ||
            node.EvaluationContinuation is { } evaluation && ReferencesResult(evaluation, resultName) ||
            node.WhenTrue is { } whenTrue && ReferencesResult(whenTrue, resultName) ||
            node.WhenFalse is { } whenFalse && ReferencesResult(whenFalse, resultName) ||
            Normalize(node.SwitchSections).Any(section => ReferencesResult(section.Branch, resultName)) ||
            node.SwitchContinuation is { } continuation && ReferencesResult(continuation, resultName);
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
                mapping.NonNullSourceName,
                names),
            PostMemberControlFlow = mapping.PostMemberControlFlow is
                    { } postMemberControlFlow
                ? LowerMemberNode(postMemberControlFlow, mapping.NonNullSourceName, names)
                : null
        };
    }

    private static TypeMapperMemberControlFlowNode LowerMemberNode(
        TypeMapperMemberControlFlowNode node,
        string sourceName,
        GeneratedLocalNameAllocator names)
    {
        if (node.EvaluationContinuation is
                { } evaluationContinuation)
        {
            var continuation = LowerMemberNode(
                evaluationContinuation,
                sourceName,
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
                ? LowerMemberNode(switchContinuation, sourceName, names)
                : null;

            return node with
            {
                SwitchSections = Normalize(node.SwitchSections)
                    .Select(section => section with
                    {
                        Branch = LowerMemberNode(
                            section.Branch,
                            sourceName,
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
                    sourceName,
                    names.Clone()),
                WhenFalse = LowerMemberNode(
                    node.WhenFalse!,
                    sourceName,
                    names.Clone())
            };
        }

        return node with
        {
            MemberMappings = LowerMembers(
                node.MemberMappings,
                sourceName,
                names)
        };
    }

    private static ImmutableArray<TypeMapperMemberMappingModel>
        LowerMembers(
        ImmutableArray<TypeMapperMemberMappingModel> mappings,
        string sourceName,
        GeneratedLocalNameAllocator names)
    {
        // Only result-dependent values need to observe the destination before
        // the first assignment. Ordinary member rules stay direct assignments.
        var preserveInitialResult = mappings.Length > 1 &&
            mappings.Any(static mapping => mapping.IsResultDependent);
        return Normalize(mappings)
            .Select(mapping =>
            {
                if (preserveInitialResult && mapping.ValueLocalName is null)
                {
                    mapping = mapping with
                    {
                        ExplicitValueExpression = mapping.SourceValueLocalName ?? mapping.ExplicitValueExpression ??
                            mapping.ConventionValueExpression?.Render(names) ??
                            sourceName + "." + (SyntaxFacts.GetKeywordKind(mapping.SourceMemberName) != SyntaxKind.None
                                ? "@" : string.Empty) + mapping.SourceMemberName,
                        ValueLocalName = AllocateValueLocalName(names, mapping.DestinationMemberName),
                        SourceValueLocalName = null,
                        ConventionValueExpression = null
                    };
                }

                if (mapping.ExplicitValueExpression is not
                        { } expression ||
                    !TryLowerMapInvocation(
                        expression,
                        mapping.DestinationMemberName,
                        forceSourceLocal: false,
                        names,
                        out var lowered))
                {
                    return FormatMemberExpression(mapping);
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

    private static TypeMapperMemberMappingModel FormatMemberExpression(
        TypeMapperMemberMappingModel mapping)
    {
        return mapping.ExplicitValueExpression is { } expression && IsComplexExpression(expression)
            ? mapping with
            {
                ExplicitValueExpression = FormatComplexExpression(
                    SyntaxFactory.ParseExpression(expression))
            }
            : mapping;
    }

    private static ImmutableArray<TypeMapperConstructorArgumentMappingModel> LowerArguments(
        ImmutableArray<TypeMapperConstructorArgumentMappingModel> arguments,
        string sourceName,
        GeneratedLocalNameAllocator names)
    {
        var lowered = arguments.Select(argument => LowerArgument(argument, names)).ToArray();
        var lastEvaluation = Array.FindLastIndex(lowered, static argument =>
            argument.ValueLocalName is not null || !Normalize(argument.EvaluationLocals).IsEmpty);

        // Extracting a later argument must not move it ahead of earlier calls,
        // property reads or user-defined conversions.
        for (var index = 0; index < lastEvaluation; index++)
        {
            var argument = lowered[index];
            if (argument.ValueLocalName is not null ||
                argument.ExplicitValueExpression == sourceName ||
                argument.ExplicitValueExpression is { } expression &&
                SyntaxFactory.ParseExpression(expression) is LiteralExpressionSyntax &&
                argument.ParameterSymbol?.Type.SpecialType is
                    not null and not SpecialType.None and not SpecialType.System_Object)
            {
                continue;
            }

            lowered[index] = argument with
            {
                ValueLocalName = AllocateValueLocalName(names, argument.ParameterName),
                ValueLocalTypeName = argument.ValueLocalTypeName ?? argument.TargetTypeName
            };
        }

        return lowered.ToImmutableArray();
    }

    private static TypeMapperConstructorArgumentMappingModel LowerArgument(
        TypeMapperConstructorArgumentMappingModel argument,
        GeneratedLocalNameAllocator names)
    {
        if (argument.ExplicitValueExpression is not { } expression)
        {
            return argument;
        }

        if (TryLowerMapInvocation(
                expression,
                argument.ParameterName,
                forceSourceLocal: false,
                names,
                out var lowered))
        {
            return argument with
            {
                ExplicitValueExpression = lowered.Expression,
                EvaluationLocals = Normalize(argument.EvaluationLocals)
                    .AddRange(lowered.Locals)
            };
        }

        if (argument.ValueLocalName is null && IsComplexExpression(expression))
        {
            var syntax = SyntaxFactory.ParseExpression(expression);
            var valueType = argument.ValueLocalTypeName ?? argument.TargetTypeName;
            return argument with
            {
                ExplicitValueExpression = FormatComplexExpression(syntax),
                ValueLocalName = AllocateValueLocalName(names, argument.ParameterName),
                ValueLocalTypeName = UnwrapParentheses(syntax) is CastExpressionSyntax cast &&
                    Normalize(cast.Type) == valueType ? "var" : valueType
            };
        }

        return argument;
    }

    private static bool IsComplexExpression(string expression)
    {
        if (expression.Length <= 100) return false;

        var evaluatedNodes = SyntaxFactory.ParseExpression(expression).DescendantNodesAndSelf(
            static node => node is not AnonymousFunctionExpressionSyntax).ToArray();
        return evaluatedNodes.Any(static node => node is ConditionalExpressionSyntax or SwitchExpressionSyntax) ||
            evaluatedNodes.OfType<InvocationExpressionSyntax>().Skip(1).Any();
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
        InvocationExpressionSyntax invocation,
        bool formatArguments = false)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return Normalize(invocation);
        }

        var lines = new List<string>
        {
            Normalize(invocation.Expression) + "("
        };
        var arguments = invocation.ArgumentList.Arguments;

        for (var index = 0; index < arguments.Count; index++)
        {
            var argument = formatArguments
                ? arguments[index].WithExpression(SyntaxFactory.ParseExpression(
                    FormatComplexExpression(arguments[index].Expression))).ToFullString().Trim()
                : Normalize(arguments[index]);
            lines.Add(
                "    " + argument.Replace("\n", "\n    ") +
                (index == arguments.Count - 1 ? ")" : ","));
        }

        return string.Join("\n", lines);
    }

    private static string FormatComplexExpression(
        ExpressionSyntax expression)
    {
        return expression switch
        {
            SwitchExpressionSyntax switchExpression => FormatSwitchExpression(switchExpression),
            ConditionalExpressionSyntax conditional when Normalize(expression).Length > 100 =>
                Normalize(conditional.Condition) + "\n    ? " + Normalize(conditional.WhenTrue) +
                "\n    : " + Normalize(conditional.WhenFalse),
            InvocationExpressionSyntax invocation when Normalize(expression).Length > 100 =>
                FormatInvocation(invocation, formatArguments: true),
            ParenthesizedExpressionSyntax parenthesized =>
                "(" + FormatComplexExpression(parenthesized.Expression) + ")",
            CastExpressionSyntax cast =>
                "(" + Normalize(cast.Type) + ")" + FormatComplexExpression(cast.Expression),
            // Keep compiling against Roslyn 4.4 while formatting collection
            // expressions when the generator runs in a C# 12 host.
            _ when expression.Kind().ToString() == "CollectionExpression" =>
                "[\n" + string.Join(",\n", expression.ChildNodes().Select(element =>
                {
                    var value = element.ChildNodes().OfType<ExpressionSyntax>().Single();
                    var spread = Normalize(element).StartsWith("..", StringComparison.Ordinal) ? ".." : string.Empty;
                    return "    " + spread + FormatComplexExpression(value).Replace("\n", "\n    ");
                })) + "\n]",
            _ => Normalize(expression)
        };
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
