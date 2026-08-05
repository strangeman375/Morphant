using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DeclarativeDependencyGraphOptimizer
{
    public static TypeMapperMappingModel Optimize(
        TypeMapperMappingModel mapping,
        INamedTypeSymbol mapperType)
    {
        var usedNames = UserResultMappingPlanner.BuildUsedLocalNames(
            mapperType);
        CollectDeclaredNames(mapping, usedNames);
        var allocator = new DependencyLocalNameAllocator(usedNames);

        if (mapping.ControlFlow is { } controlFlow)
        {
            var mapNew = OptimizeNode(
                controlFlow.MapNewRoot,
                mapNew: true,
                new Dictionary<string, string>(StringComparer.Ordinal),
                allocator);
            var mapExisting = OptimizeNode(
                controlFlow.MapExistingRoot,
                mapNew: false,
                new Dictionary<string, string>(StringComparer.Ordinal),
                allocator);

            return !mapNew.Changed && !mapExisting.Changed
                ? mapping
                : mapping with
                {
                    ControlFlow = new TypeMapperControlFlowMappingModel(
                        mapNew.Node,
                        mapExisting.Node)
                };
        }

        var createRoot = LeafNode(mapping);
        var existingRoot = LeafNode(
            mapping with
            {
                MapNewDirectExpression = null,
                MapNewFactory = null,
                MapNewConstructor = null,
                MapNewMemberMappings = [],
                MapNewPostMemberMappings = []
            });
        var optimizedCreate = OptimizeNode(
            createRoot,
            mapNew: true,
            new Dictionary<string, string>(StringComparer.Ordinal),
            allocator);
        var optimizedExisting = OptimizeNode(
            existingRoot,
            mapNew: false,
            new Dictionary<string, string>(StringComparer.Ordinal),
            allocator);

        return !optimizedCreate.Changed && !optimizedExisting.Changed
            ? mapping
            : mapping with
            {
                ControlFlow = new TypeMapperControlFlowMappingModel(
                    optimizedCreate.Node,
                    optimizedExisting.Node)
            };
    }

    private static NodeOptimizationResult OptimizeNode(
        TypeMapperControlFlowNode node,
        bool mapNew,
        Dictionary<string, string> environment,
        DependencyLocalNameAllocator allocator)
    {
        var changed = false;
        var locals = ImmutableArray.CreateBuilder<TypeMapperLocalValueModel>();
        var bodyKeys = CollectBodyKeys(node, mapNew);

        for (var index = 0; index < node.Locals.Length; index++)
        {
            var local = node.Locals[index];
            var later = new HashSet<string>(
                bodyKeys,
                StringComparer.Ordinal);

            for (var next = index + 1;
                 next < node.Locals.Length;
                 next++)
            {
                AddExpressionKeys(
                    node.Locals[next].DependencyExpression,
                    later);
            }

            if (local.DependencyExpression is { } dependency)
            {
                var optimized = OptimizeExpression(
                    dependency,
                    environment,
                    later,
                    allocator,
                    "sharedValue",
                    CanStoreRoot(local, dependency)
                        ? local.Name
                        : null);

                locals.AddRange(optimized.PrefixLocals);
                local = local with
                {
                    ValueExpression = optimized.Expression,
                    DependencyExpression = null
                };
                changed |= optimized.Changed;
            }

            locals.Add(local);

            if (local.DeclaredValueKey is { } declaredKey)
            {
                environment[declaredKey] = local.Name;
            }
        }

        var optimizedLocals = locals.ToImmutable();

        if (!optimizedLocals.SequenceEqual(node.Locals))
        {
            changed = true;
        }

        node = node with { Locals = optimizedLocals };

        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            var later = CollectKeys(evaluationContinuation, mapNew);
            var expression = OptimizeExpressionSlot(
                node.EvaluationExpression!,
                node.EvaluationDependency,
                environment,
                later,
                allocator,
                "sharedEvaluation");
            var continuation = OptimizeNode(
                evaluationContinuation,
                mapNew,
                environment,
                allocator);

            return new NodeOptimizationResult(
                node with
                {
                    Locals = node.Locals.AddRange(
                        expression.PrefixLocals),
                    EvaluationExpression = expression.Expression,
                    EvaluationDependency = null,
                    EvaluationContinuation = continuation.Node
                },
                changed || expression.Changed || continuation.Changed);
        }

        if (node.SwitchExpression is not null)
        {
            var later = new HashSet<string>(StringComparer.Ordinal);

            foreach (var section in node.SwitchSections)
            {
                later.UnionWith(CollectKeys(section.Branch, mapNew));
            }

            if (node.SwitchContinuation is { } switchContinuation)
            {
                later.UnionWith(CollectKeys(switchContinuation, mapNew));
            }

            var expression = OptimizeExpressionSlot(
                node.SwitchExpression,
                node.SwitchDependency,
                environment,
                later,
                allocator,
                "sharedSwitchValue");
            var sections = node.SwitchSections.Select(section =>
            {
                var branch = OptimizeNode(
                    section.Branch,
                    mapNew,
                    Clone(environment),
                    allocator);
                changed |= branch.Changed;
                return section with { Branch = branch.Node };
            }).ToImmutableArray();
            TypeMapperControlFlowNode? continuation = null;

            if (node.SwitchContinuation is { } currentContinuation)
            {
                var optimized = OptimizeNode(
                    currentContinuation,
                    mapNew,
                    Clone(environment),
                    allocator);
                continuation = optimized.Node;
                changed |= optimized.Changed;
            }

            return new NodeOptimizationResult(
                node with
                {
                    Locals = node.Locals.AddRange(
                        expression.PrefixLocals),
                    SwitchExpression = expression.Expression,
                    SwitchDependency = null,
                    SwitchSections = sections,
                    SwitchContinuation = continuation
                },
                changed || expression.Changed);
        }

        if (node.Condition is not null)
        {
            var later = CollectKeys(node.WhenTrue!, mapNew);
            later.UnionWith(CollectKeys(node.WhenFalse!, mapNew));
            var expression = OptimizeExpressionSlot(
                node.Condition,
                node.ConditionDependency,
                environment,
                later,
                allocator,
                "sharedCondition");
            var whenTrue = OptimizeNode(
                node.WhenTrue!,
                mapNew,
                Clone(environment),
                allocator);
            var whenFalse = OptimizeNode(
                node.WhenFalse!,
                mapNew,
                Clone(environment),
                allocator);

            return new NodeOptimizationResult(
                node with
                {
                    Locals = node.Locals.AddRange(
                        expression.PrefixLocals),
                    Condition = expression.Expression,
                    ConditionDependency = null,
                    WhenTrue = whenTrue.Node,
                    WhenFalse = whenFalse.Node
                },
                changed || expression.Changed ||
                whenTrue.Changed || whenFalse.Changed);
        }

        if (node.ThrowExpression is not null)
        {
            var expression = OptimizeExpressionSlot(
                node.ThrowExpression,
                node.ThrowDependency,
                environment,
                new HashSet<string>(StringComparer.Ordinal),
                allocator,
                "sharedException");

            return new NodeOptimizationResult(
                node with
                {
                    Locals = node.Locals.AddRange(
                        expression.PrefixLocals),
                    ThrowExpression = expression.Expression,
                    ThrowDependency = null
                },
                changed || expression.Changed);
        }

        if (node.Leaf is not { } leaf)
        {
            return new NodeOptimizationResult(node, changed);
        }

        var leafResult = OptimizeLeaf(
            leaf,
            mapNew,
            environment,
            allocator);

        return new NodeOptimizationResult(
            node with { Leaf = leafResult.Mapping },
            changed || leafResult.Changed);
    }

    private static LeafOptimizationResult OptimizeLeaf(
        TypeMapperMappingModel mapping,
        bool mapNew,
        Dictionary<string, string> environment,
        DependencyLocalNameAllocator allocator)
    {
        var changed = false;
        var replacement = !mapNew &&
            (mapping.MapNewConstructor is not null ||
             mapping.MapNewFactory is not null ||
             mapping.MapNewDirectExpression is not null);

        if (mapNew || replacement)
        {
            if (mapping.MapNewConstructor is { } constructor)
            {
                var optimizedConstructor = OptimizeConstructor(
                    constructor,
                    mapping.MapNewMemberMappings,
                    mapping.MapNewPostMemberMappings,
                    mapping.PostMemberControlFlow,
                    environment,
                    allocator);
                mapping = mapping with
                {
                    MapNewConstructor = optimizedConstructor.Constructor,
                    MapNewMemberMappings =
                        optimizedConstructor.InitializerMappings,
                    MapNewPostMemberMappings =
                        optimizedConstructor.PostMappings,
                    PostMemberControlFlow =
                        optimizedConstructor.PostControlFlow
                };
                changed |= optimizedConstructor.Changed;
            }
            else
            {
                var post = OptimizePostMappings(
                    mapping.MapNewPostMemberMappings,
                    mapping.PostMemberControlFlow,
                    environment,
                    allocator);
                mapping = mapping with
                {
                    MapNewPostMemberMappings = post.Mappings,
                    PostMemberControlFlow = post.ControlFlow
                };
                changed |= post.Changed;
            }
        }
        else
        {
            var post = OptimizePostMappings(
                mapping.MapExistingMemberMappings,
                mapping.PostMemberControlFlow,
                environment,
                allocator);
            mapping = mapping with
            {
                MapExistingMemberMappings = post.Mappings,
                PostMemberControlFlow = post.ControlFlow
            };
            changed |= post.Changed;
        }

        return new LeafOptimizationResult(mapping, changed);
    }

    private static ConstructorOptimizationResult OptimizeConstructor(
        TypeMapperConstructorMappingModel constructor,
        ImmutableArray<TypeMapperMemberMappingModel> initializerMappings,
        ImmutableArray<TypeMapperMemberMappingModel> postMappings,
        TypeMapperMemberControlFlowNode? postControlFlow,
        Dictionary<string, string> environment,
        DependencyLocalNameAllocator allocator)
    {
        var changed = false;
        var arguments = constructor.Arguments.ToArray();
        var lastPreEvaluatedArgument = -1;
        var initializerKeys = CollectMemberKeys(initializerMappings);
        var postKeys = CollectMemberKeys(postMappings);

        if (postControlFlow is not null)
        {
            postKeys.UnionWith(CollectKeys(postControlFlow));
        }

        for (var index = 0; index < arguments.Length; index++)
        {
            var argument = arguments[index];

            if (argument.DependencyExpression is not { } dependency)
            {
                continue;
            }

            var later = new HashSet<string>(
                initializerKeys,
                StringComparer.Ordinal);
            later.UnionWith(postKeys);

            for (var next = index + 1;
                 next < arguments.Length;
                 next++)
            {
                AddExpressionKeys(
                    arguments[next].DependencyExpression,
                    later);
            }

            var optimized = OptimizeExpression(
                dependency,
                environment,
                later,
                allocator,
                "shared" + Pascal(argument.ParameterName),
                argument.ValueLocalName);
            argument = argument with
            {
                ExplicitValueExpression = optimized.Expression,
                DependencyExpression = null,
                EvaluationLocals = Normalize(
                    argument.EvaluationLocals)
                    .AddRange(optimized.PrefixLocals)
            };

            if (!optimized.PrefixLocals.IsEmpty)
            {
                lastPreEvaluatedArgument = index;
            }

            arguments[index] = argument;
            changed |= optimized.Changed;
        }

        if (lastPreEvaluatedArgument >= 0)
        {
            for (var index = 0;
                 index < lastPreEvaluatedArgument;
                 index++)
            {
                if (arguments[index].ValueLocalName is not null)
                {
                    continue;
                }

                var argument = arguments[index];
                arguments[index] = argument with
                {
                    ValueLocalName = allocator.Allocate(
                        "construct" + Pascal(argument.ParameterName))
                };
                changed = true;
            }
        }

        var valueLocals = Normalize(constructor.ValueLocals).ToBuilder();
        var initializerArray = initializerMappings.ToArray();

        for (var index = 0;
             index < initializerArray.Length;
             index++)
        {
            var member = initializerArray[index];

            if (member.DependencyExpression is not { } dependency)
            {
                continue;
            }

            var later = new HashSet<string>(postKeys, StringComparer.Ordinal);

            for (var next = index + 1;
                 next < initializerArray.Length;
                 next++)
            {
                AddExpressionKeys(
                    initializerArray[next].DependencyExpression,
                    later);
            }

            var optimized = OptimizeExpression(
                dependency,
                environment,
                later,
                allocator,
                "shared" + Pascal(member.DestinationMemberName));
            valueLocals.AddRange(optimized.PrefixLocals);
            initializerArray[index] = member with
            {
                ExplicitValueExpression = optimized.Expression,
                DependencyExpression = null
            };
            changed |= optimized.Changed;
        }

        var post = OptimizePostMappings(
            postMappings,
            postControlFlow,
            environment,
            allocator);

        return new ConstructorOptimizationResult(
            constructor with
            {
                Arguments = arguments.ToImmutableArray(),
                ValueLocals = valueLocals.ToImmutable()
            },
            initializerArray.ToImmutableArray(),
            post.Mappings,
            post.ControlFlow,
            changed || post.Changed);
    }

    private static PostOptimizationResult OptimizePostMappings(
        ImmutableArray<TypeMapperMemberMappingModel> mappings,
        TypeMapperMemberControlFlowNode? controlFlow,
        Dictionary<string, string> environment,
        DependencyLocalNameAllocator allocator)
    {
        if (controlFlow is not null)
        {
            var optimized = OptimizeMemberNode(
                controlFlow,
                environment,
                allocator);

            return new PostOptimizationResult(
                mappings,
                optimized.Node,
                optimized.Changed);
        }

        var changed = false;
        var result = mappings.ToArray();

        for (var index = 0; index < result.Length; index++)
        {
            var member = result[index];

            if (member.DependencyExpression is not { } dependency)
            {
                continue;
            }

            var later = new HashSet<string>(StringComparer.Ordinal);

            for (var next = index + 1; next < result.Length; next++)
            {
                AddExpressionKeys(
                    result[next].DependencyExpression,
                    later);
            }

            var optimized = OptimizeExpression(
                dependency,
                environment,
                later,
                allocator,
                "shared" + Pascal(member.DestinationMemberName),
                member.ValueLocalName);
            result[index] = member with
            {
                ExplicitValueExpression = optimized.Expression,
                DependencyExpression = null,
                EvaluationLocals = Normalize(member.EvaluationLocals)
                    .AddRange(optimized.PrefixLocals)
            };
            changed |= optimized.Changed;
        }

        return new PostOptimizationResult(
            result.ToImmutableArray(),
            ControlFlow: null,
            changed);
    }

    private static MemberNodeOptimizationResult OptimizeMemberNode(
        TypeMapperMemberControlFlowNode node,
        Dictionary<string, string> environment,
        DependencyLocalNameAllocator allocator)
    {
        var changed = false;
        var locals = ImmutableArray.CreateBuilder<TypeMapperLocalValueModel>();
        var bodyKeys = CollectBodyKeys(node);

        for (var index = 0; index < node.Locals.Length; index++)
        {
            var local = node.Locals[index];
            var later = new HashSet<string>(bodyKeys, StringComparer.Ordinal);

            for (var next = index + 1;
                 next < node.Locals.Length;
                 next++)
            {
                AddExpressionKeys(
                    node.Locals[next].DependencyExpression,
                    later);
            }

            if (local.DependencyExpression is { } dependency)
            {
                var optimized = OptimizeExpression(
                    dependency,
                    environment,
                    later,
                    allocator,
                    "sharedValue",
                    CanStoreRoot(local, dependency)
                        ? local.Name
                        : null);
                locals.AddRange(optimized.PrefixLocals);
                local = local with
                {
                    ValueExpression = optimized.Expression,
                    DependencyExpression = null
                };
                changed |= optimized.Changed;
            }

            locals.Add(local);

            if (local.DeclaredValueKey is { } key)
            {
                environment[key] = local.Name;
            }
        }

        node = node with { Locals = locals.ToImmutable() };

        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            var expression = OptimizeExpressionSlot(
                node.EvaluationExpression!,
                node.EvaluationDependency,
                environment,
                CollectKeys(evaluationContinuation),
                allocator,
                "sharedEvaluation");
            var continuation = OptimizeMemberNode(
                evaluationContinuation,
                environment,
                allocator);

            return new MemberNodeOptimizationResult(
                node with
                {
                    Locals = node.Locals.AddRange(
                        expression.PrefixLocals),
                    EvaluationExpression = expression.Expression,
                    EvaluationDependency = null,
                    EvaluationContinuation = continuation.Node
                },
                changed || expression.Changed || continuation.Changed);
        }

        if (node.SwitchExpression is not null)
        {
            var later = new HashSet<string>(StringComparer.Ordinal);

            foreach (var section in node.SwitchSections)
            {
                later.UnionWith(CollectKeys(section.Branch));
            }

            if (node.SwitchContinuation is { } switchContinuation)
            {
                later.UnionWith(CollectKeys(switchContinuation));
            }

            var expression = OptimizeExpressionSlot(
                node.SwitchExpression,
                node.SwitchDependency,
                environment,
                later,
                allocator,
                "sharedSwitchValue");
            var sections = node.SwitchSections.Select(section =>
            {
                var branch = OptimizeMemberNode(
                    section.Branch,
                    Clone(environment),
                    allocator);
                changed |= branch.Changed;
                return section with { Branch = branch.Node };
            }).ToImmutableArray();
            TypeMapperMemberControlFlowNode? continuation = null;

            if (node.SwitchContinuation is { } currentContinuation)
            {
                var optimized = OptimizeMemberNode(
                    currentContinuation,
                    Clone(environment),
                    allocator);
                continuation = optimized.Node;
                changed |= optimized.Changed;
            }

            return new MemberNodeOptimizationResult(
                node with
                {
                    Locals = node.Locals.AddRange(
                        expression.PrefixLocals),
                    SwitchExpression = expression.Expression,
                    SwitchDependency = null,
                    SwitchSections = sections,
                    SwitchContinuation = continuation
                },
                changed || expression.Changed);
        }

        if (node.Condition is not null)
        {
            var later = CollectKeys(node.WhenTrue!);
            later.UnionWith(CollectKeys(node.WhenFalse!));
            var expression = OptimizeExpressionSlot(
                node.Condition,
                node.ConditionDependency,
                environment,
                later,
                allocator,
                "sharedCondition");
            var whenTrue = OptimizeMemberNode(
                node.WhenTrue!,
                Clone(environment),
                allocator);
            var whenFalse = OptimizeMemberNode(
                node.WhenFalse!,
                Clone(environment),
                allocator);

            return new MemberNodeOptimizationResult(
                node with
                {
                    Locals = node.Locals.AddRange(
                        expression.PrefixLocals),
                    Condition = expression.Expression,
                    ConditionDependency = null,
                    WhenTrue = whenTrue.Node,
                    WhenFalse = whenFalse.Node
                },
                changed || expression.Changed ||
                whenTrue.Changed || whenFalse.Changed);
        }

        if (node.ThrowExpression is not null)
        {
            var expression = OptimizeExpressionSlot(
                node.ThrowExpression,
                node.ThrowDependency,
                environment,
                new HashSet<string>(StringComparer.Ordinal),
                allocator,
                "sharedException");

            return new MemberNodeOptimizationResult(
                node with
                {
                    Locals = node.Locals.AddRange(
                        expression.PrefixLocals),
                    ThrowExpression = expression.Expression,
                    ThrowDependency = null
                },
                changed || expression.Changed);
        }

        var post = OptimizePostMappings(
            node.MemberMappings,
            controlFlow: null,
            environment,
            allocator);

        return new MemberNodeOptimizationResult(
            node with
            {
                MemberMappings = post.Mappings
            },
            changed || post.Changed);
    }

    private static ExpressionOptimizationResult OptimizeExpressionSlot(
        string expression,
        TypeMapperDependencyExpressionModel? dependency,
        Dictionary<string, string> environment,
        HashSet<string> later,
        DependencyLocalNameAllocator allocator,
        string preferredName)
    {
        return dependency is null
            ? new ExpressionOptimizationResult(
                expression,
                [],
                Changed: false)
            : OptimizeExpression(
                dependency,
                environment,
                later,
                allocator,
                preferredName);
    }

    private static ExpressionOptimizationResult OptimizeExpression(
        TypeMapperDependencyExpressionModel expression,
        Dictionary<string, string> environment,
        HashSet<string> later,
        DependencyLocalNameAllocator allocator,
        string preferredName,
        string? rootStorageName = null)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        CountKeys(expression.Root, counts);
        var prefix = ImmutableArray.CreateBuilder<TypeMapperLocalValueModel>();
        var deferredValues =
            new List<KeyValuePair<string, string>>();
        var changed = false;

        bool WillAddPrefix(
            TypeMapperDependencyExpressionNodeModel node)
        {
            if (environment.ContainsKey(node.Key))
            {
                return false;
            }

            if (node.CanMaterialize &&
                counts[node.Key] == 1 &&
                later.Contains(node.Key))
            {
                return true;
            }

            return node.Children.Any(child =>
                WillAddPrefix(child.Node));
        }

        RenderedDependencyNode Render(
            TypeMapperDependencyExpressionNodeModel node,
            bool isRoot)
        {
            if (environment.TryGetValue(node.Key, out var existing))
            {
                changed = true;
                return new RenderedDependencyNode(
                    existing,
                    IsMaterialized: true);
            }

            var rendered = node.Template;
            var childExpressions = new string[node.Children.Length];
            var childMaterialized = new bool[node.Children.Length];

            for (var index = 0;
                 index < node.Children.Length;
                 index++)
            {
                var child = node.Children[index];

                if (WillAddPrefix(child.Node))
                {
                    for (var previous = 0;
                         previous < index;
                         previous++)
                    {
                        var previousNode =
                            node.Children[previous].Node;

                        if (childMaterialized[previous] ||
                            !previousNode.CanMaterialize)
                        {
                            continue;
                        }

                        var evaluationName = allocator.Allocate(
                            "evaluatedValue");
                        prefix.Add(
                            new TypeMapperLocalValueModel(
                                previousNode.ValueTypeName,
                                evaluationName,
                                childExpressions[previous],
                                IsConst: false));
                        childExpressions[previous] = evaluationName;
                        childMaterialized[previous] = true;
                        if (counts[previousNode.Key] == 1)
                        {
                            deferredValues.Add(
                                new KeyValuePair<string, string>(
                                    previousNode.Key,
                                    evaluationName));
                        }
                        changed = true;
                    }
                }

                var childResult = Render(
                    child.Node,
                    isRoot: false);
                childExpressions[index] = childResult.Expression;
                childMaterialized[index] =
                    childResult.IsMaterialized;
            }

            for (var index = 0;
                 index < node.Children.Length;
                 index++)
            {
                rendered = rendered.Replace(
                    node.Children[index].Placeholder,
                    childExpressions[index]);
            }

            var canShare = node.CanMaterialize &&
                counts[node.Key] == 1 &&
                later.Contains(node.Key);

            if (isRoot && rootStorageName is not null)
            {
                environment[node.Key] = rootStorageName;
                return new RenderedDependencyNode(
                    rendered,
                    IsMaterialized: true);
            }

            if (!canShare)
            {
                return new RenderedDependencyNode(
                    rendered,
                    IsMaterialized: false);
            }

            var localName = allocator.Allocate(
                isRoot
                    ? preferredName
                    : "sharedValue");
            prefix.Add(
                new TypeMapperLocalValueModel(
                    node.ValueTypeName,
                    localName,
                    rendered,
                    IsConst: false));
            environment[node.Key] = localName;
            changed = true;
            return new RenderedDependencyNode(
                localName,
                IsMaterialized: true);
        }

        var result = Render(
            expression.Root,
            isRoot: true);

        foreach (var value in deferredValues)
        {
            if (!environment.ContainsKey(value.Key))
            {
                environment.Add(value.Key, value.Value);
            }
        }

        return new ExpressionOptimizationResult(
            result.Expression,
            prefix.ToImmutable(),
            changed);
    }

    private static HashSet<string> CollectBodyKeys(
        TypeMapperControlFlowNode node,
        bool mapNew)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        AddExpressionKeys(node.EvaluationDependency, result);
        AddExpressionKeys(node.SwitchDependency, result);
        AddExpressionKeys(node.ConditionDependency, result);
        AddExpressionKeys(node.ThrowDependency, result);

        if (node.EvaluationContinuation is { } evaluation)
        {
            result.UnionWith(CollectKeys(evaluation, mapNew));
        }
        else if (node.SwitchExpression is not null)
        {
            foreach (var section in node.SwitchSections)
            {
                result.UnionWith(CollectKeys(section.Branch, mapNew));
            }

            if (node.SwitchContinuation is { } continuation)
            {
                result.UnionWith(CollectKeys(continuation, mapNew));
            }
        }
        else if (node.Condition is not null)
        {
            result.UnionWith(CollectKeys(node.WhenTrue!, mapNew));
            result.UnionWith(CollectKeys(node.WhenFalse!, mapNew));
        }
        else if (node.Leaf is { } leaf)
        {
            result.UnionWith(CollectLeafKeys(leaf, mapNew));
        }

        return result;
    }

    private static HashSet<string> CollectKeys(
        TypeMapperControlFlowNode node,
        bool mapNew)
    {
        var result = CollectBodyKeys(node, mapNew);

        foreach (var local in node.Locals)
        {
            AddExpressionKeys(local.DependencyExpression, result);
        }

        return result;
    }

    private static HashSet<string> CollectBodyKeys(
        TypeMapperMemberControlFlowNode node)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        AddExpressionKeys(node.EvaluationDependency, result);
        AddExpressionKeys(node.SwitchDependency, result);
        AddExpressionKeys(node.ConditionDependency, result);
        AddExpressionKeys(node.ThrowDependency, result);

        if (node.EvaluationContinuation is { } evaluation)
        {
            result.UnionWith(CollectKeys(evaluation));
        }
        else if (node.SwitchExpression is not null)
        {
            foreach (var section in node.SwitchSections)
            {
                result.UnionWith(CollectKeys(section.Branch));
            }

            if (node.SwitchContinuation is { } continuation)
            {
                result.UnionWith(CollectKeys(continuation));
            }
        }
        else if (node.Condition is not null)
        {
            result.UnionWith(CollectKeys(node.WhenTrue!));
            result.UnionWith(CollectKeys(node.WhenFalse!));
        }
        else
        {
            result.UnionWith(CollectMemberKeys(node.MemberMappings));
        }

        return result;
    }

    private static HashSet<string> CollectKeys(
        TypeMapperMemberControlFlowNode node)
    {
        var result = CollectBodyKeys(node);

        foreach (var local in node.Locals)
        {
            AddExpressionKeys(local.DependencyExpression, result);
        }

        return result;
    }

    private static HashSet<string> CollectLeafKeys(
        TypeMapperMappingModel mapping,
        bool mapNew)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var replacement = !mapNew &&
            (mapping.MapNewConstructor is not null ||
             mapping.MapNewFactory is not null ||
             mapping.MapNewDirectExpression is not null);

        if ((mapNew || replacement) &&
            mapping.MapNewConstructor is { } constructor)
        {
            foreach (var argument in constructor.Arguments)
            {
                AddExpressionKeys(argument.DependencyExpression, result);
            }

            result.UnionWith(
                CollectMemberKeys(mapping.MapNewMemberMappings));
            result.UnionWith(
                CollectMemberKeys(mapping.MapNewPostMemberMappings));
        }
        else if (mapNew || replacement)
        {
            result.UnionWith(
                CollectMemberKeys(mapping.MapNewPostMemberMappings));
        }
        else
        {
            result.UnionWith(
                CollectMemberKeys(mapping.MapExistingMemberMappings));
        }

        if (mapping.PostMemberControlFlow is { } controlFlow)
        {
            result.UnionWith(CollectKeys(controlFlow));
        }

        return result;
    }

    private static HashSet<string> CollectMemberKeys(
        ImmutableArray<TypeMapperMemberMappingModel> mappings)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var mapping in mappings)
        {
            AddExpressionKeys(mapping.DependencyExpression, result);
        }

        return result;
    }

    private static void AddExpressionKeys(
        TypeMapperDependencyExpressionModel? expression,
        HashSet<string> result)
    {
        if (expression is null)
        {
            return;
        }

        AddNodeKeys(expression.Root, result);
    }

    private static void AddNodeKeys(
        TypeMapperDependencyExpressionNodeModel node,
        HashSet<string> result)
    {
        result.Add(node.Key);

        foreach (var child in node.Children)
        {
            AddNodeKeys(child.Node, result);
        }
    }

    private static void CountKeys(
        TypeMapperDependencyExpressionNodeModel node,
        Dictionary<string, int> result)
    {
        result.TryGetValue(node.Key, out var count);
        result[node.Key] = count + 1;

        foreach (var child in node.Children)
        {
            CountKeys(child.Node, result);
        }
    }

    private static bool CanStoreRoot(
        TypeMapperLocalValueModel local,
        TypeMapperDependencyExpressionModel dependency) =>
        StringComparer.Ordinal.Equals(
            local.StoredValueTypeName,
            dependency.Root.ValueTypeName);

    private static TypeMapperControlFlowNode LeafNode(
        TypeMapperMappingModel mapping)
    {
        return new TypeMapperControlFlowNode(
            Locals: [],
            Condition: null,
            WhenTrue: null,
            WhenFalse: null,
            Leaf: mapping with { ControlFlow = null },
            ThrowExpression: null);
    }

    private static Dictionary<string, string> Clone(
        Dictionary<string, string> environment) =>
        new(environment, StringComparer.Ordinal);

    private static ImmutableArray<TypeMapperLocalValueModel> Normalize(
        ImmutableArray<TypeMapperLocalValueModel> values) =>
        values.IsDefault
            ? []
            : values;

    private static string Pascal(string value)
    {
        return string.IsNullOrEmpty(value)
            ? "Value"
            : char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    private static void CollectDeclaredNames(
        TypeMapperMappingModel mapping,
        HashSet<string> names)
    {
        names.Add(mapping.NonNullSourceName);
        names.Add(mapping.ResultLocalName);
        names.Add("destination");

        if (mapping.ControlFlow is { } controlFlow)
        {
            CollectDeclaredNames(controlFlow.MapNewRoot, names);
            CollectDeclaredNames(controlFlow.MapExistingRoot, names);
        }
        else
        {
            CollectLeafDeclaredNames(mapping, names);
        }
    }

    private static void CollectDeclaredNames(
        TypeMapperControlFlowNode node,
        HashSet<string> names)
    {
        AddIdentifiers(node.Condition, names);
        AddIdentifiers(node.SwitchExpression, names);
        AddIdentifiers(node.EvaluationExpression, names);
        AddIdentifiers(node.ThrowExpression, names);

        foreach (var local in node.Locals)
        {
            names.Add(local.Name);
        }

        foreach (var section in node.SwitchSections.IsDefault
                     ? []
                     : node.SwitchSections)
        {
            foreach (var label in section.Labels)
            {
                AddIdentifiers(label, names);
            }
        }

        if (node.Leaf is { } leaf)
        {
            CollectLeafDeclaredNames(leaf, names);
            return;
        }

        if (node.EvaluationContinuation is { } evaluation)
        {
            CollectDeclaredNames(evaluation, names);
        }

        foreach (var section in node.SwitchSections.IsDefault
                     ? []
                     : node.SwitchSections)
        {
            CollectDeclaredNames(section.Branch, names);
        }

        if (node.SwitchContinuation is { } continuation)
        {
            CollectDeclaredNames(continuation, names);
        }

        if (node.WhenTrue is { } whenTrue)
        {
            CollectDeclaredNames(whenTrue, names);
        }

        if (node.WhenFalse is { } whenFalse)
        {
            CollectDeclaredNames(whenFalse, names);
        }
    }

    private static void CollectDeclaredNames(
        TypeMapperMemberControlFlowNode node,
        HashSet<string> names)
    {
        AddIdentifiers(node.Condition, names);
        AddIdentifiers(node.SwitchExpression, names);
        AddIdentifiers(node.EvaluationExpression, names);
        AddIdentifiers(node.ThrowExpression, names);

        foreach (var local in node.Locals)
        {
            names.Add(local.Name);
        }

        foreach (var section in node.SwitchSections.IsDefault
                     ? []
                     : node.SwitchSections)
        {
            foreach (var label in section.Labels)
            {
                AddIdentifiers(label, names);
            }
        }

        foreach (var member in node.MemberMappings)
        {
            AddMemberLocalNames(member, names);
        }

        if (node.EvaluationContinuation is { } evaluation)
        {
            CollectDeclaredNames(evaluation, names);
        }

        foreach (var section in node.SwitchSections.IsDefault
                     ? []
                     : node.SwitchSections)
        {
            CollectDeclaredNames(section.Branch, names);
        }

        if (node.SwitchContinuation is { } continuation)
        {
            CollectDeclaredNames(continuation, names);
        }

        if (node.WhenTrue is { } whenTrue)
        {
            CollectDeclaredNames(whenTrue, names);
        }

        if (node.WhenFalse is { } whenFalse)
        {
            CollectDeclaredNames(whenFalse, names);
        }
    }

    private static void CollectLeafDeclaredNames(
        TypeMapperMappingModel mapping,
        HashSet<string> names)
    {
        if (mapping.MapNewFactory is { } factory)
        {
            names.Add(factory.DestinationLocalName);

            if (factory.NullableValueLocalName is { } nullableValueLocal)
            {
                names.Add(nullableValueLocal);
            }
        }

        if (mapping.MapNewConstructor is { } constructor)
        {
            AddLocalNames(constructor.ValueLocals, names);

            foreach (var argument in constructor.Arguments)
            {
                if (argument.ValueLocalName is { } valueLocal)
                {
                    names.Add(valueLocal);
                }

                AddLocalNames(argument.EvaluationLocals, names);
            }
        }

        foreach (var member in mapping.MapNewMemberMappings
                     .AddRange(mapping.MapNewPostMemberMappings)
                     .AddRange(mapping.MapExistingMemberMappings))
        {
            AddMemberLocalNames(member, names);
        }

        if (mapping.PostMemberControlFlow is { } postControlFlow)
        {
            CollectDeclaredNames(postControlFlow, names);
        }
    }

    private static void AddLocalNames(
        ImmutableArray<TypeMapperLocalValueModel> locals,
        HashSet<string> names)
    {
        foreach (var local in Normalize(locals))
        {
            names.Add(local.Name);
        }
    }

    private static void AddMemberLocalNames(
        TypeMapperMemberMappingModel member,
        HashSet<string> names)
    {
        if (member.SourceValueLocalName is { } sourceValueLocal)
        {
            names.Add(sourceValueLocal);
        }

        if (member.ValueLocalName is { } valueLocal)
        {
            names.Add(valueLocal);
        }

        AddLocalNames(member.EvaluationLocals, names);
    }

    private static void AddIdentifiers(
        string? syntax,
        HashSet<string> names)
    {
        if (syntax is null)
        {
            return;
        }

        foreach (var token in SyntaxFactory.ParseTokens(syntax))
        {
            if (token.IsKind(SyntaxKind.IdentifierToken))
            {
                names.Add(token.ValueText);
            }
        }
    }

    private sealed class DependencyLocalNameAllocator
    {
        private readonly HashSet<string> _usedNames;

        public DependencyLocalNameAllocator(HashSet<string> usedNames)
        {
            _usedNames = usedNames;
        }

        public string Allocate(string preferredName) =>
            UserResultMappingPlanner.AllocateName(
                preferredName,
                _usedNames);
    }

    private readonly record struct NodeOptimizationResult(
        TypeMapperControlFlowNode Node,
        bool Changed);

    private readonly record struct MemberNodeOptimizationResult(
        TypeMapperMemberControlFlowNode Node,
        bool Changed);

    private readonly record struct LeafOptimizationResult(
        TypeMapperMappingModel Mapping,
        bool Changed);

    private readonly record struct ConstructorOptimizationResult(
        TypeMapperConstructorMappingModel Constructor,
        ImmutableArray<TypeMapperMemberMappingModel> InitializerMappings,
        ImmutableArray<TypeMapperMemberMappingModel> PostMappings,
        TypeMapperMemberControlFlowNode? PostControlFlow,
        bool Changed);

    private readonly record struct PostOptimizationResult(
        ImmutableArray<TypeMapperMemberMappingModel> Mappings,
        TypeMapperMemberControlFlowNode? ControlFlow,
        bool Changed);

    private readonly record struct ExpressionOptimizationResult(
        string Expression,
        ImmutableArray<TypeMapperLocalValueModel> PrefixLocals,
        bool Changed);

    private readonly record struct RenderedDependencyNode(
        string Expression,
        bool IsMaterialized);
}
