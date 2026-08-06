using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class DeclarativeControlFlowLowerer
{
    public static TypeMapperControlFlowNode PreserveLocalNames(
        TypeMapperControlFlowNode node)
    {
        var locals = node.Locals.Select(local =>
                local with { IsSynthetic = true })
            .ToImmutableArray();

        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            return node with
            {
                Locals = locals,
                EvaluationContinuation = PreserveLocalNames(
                    evaluationContinuation)
            };
        }

        if (node.SwitchExpression is not null)
        {
            return node with
            {
                Locals = locals,
                SwitchSections = node.SwitchSections.Select(section =>
                        section with
                        {
                            Branch = PreserveLocalNames(section.Branch)
                        })
                    .ToImmutableArray(),
                SwitchContinuation = node.SwitchContinuation is
                    { } continuation
                    ? PreserveLocalNames(continuation)
                    : null
            };
        }

        if (node.Condition is not null)
        {
            return node with
            {
                Locals = locals,
                WhenTrue = PreserveLocalNames(node.WhenTrue!),
                WhenFalse = PreserveLocalNames(node.WhenFalse!)
            };
        }

        return node with { Locals = locals };
    }

    public static bool TryBuildMemberControlFlow(
        DeclarativeControlFlowProgram program,
        SemanticModel semanticModel,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        IParameterSymbol? resultParameter,
        string? resultName,
        SyntaxNode transferScope,
        TypeMapperMappingModel mapping,
        Func<DeclarativeLeafSyntaxNode,
            ImmutableArray<TypeMapperMemberMappingModel>> buildLeaf,
        CancellationToken cancellationToken,
        out TypeMapperMemberControlFlowNode root)
    {
        TypeMapperControlFlowNode BuildLeaf(
            DeclarativeLeafSyntaxNode leaf)
        {
            var leafMapping = mapping with
            {
                MapNewDirectExpression = null,
                MapExistingDirectExpression = null,
                MapNewFactory = null,
                MapNewConstructor = null,
                MapNewMemberMappings = [],
                MapNewPostMemberMappings = [],
                MapExistingMemberMappings = buildLeaf(leaf),
                ControlFlow = null,
                MapNewUnsupportedExceptionMessage = null,
                MapExistingUnsupportedExceptionMessage = null,
                UnsupportedExceptionMessage = null,
                PostMemberControlFlow = null
            };

            return new TypeMapperControlFlowNode(
                Locals: [],
                Condition: null,
                WhenTrue: null,
                WhenFalse: null,
                leafMapping,
                ThrowExpression: null);
        }

        if (!TryBuild(
                program,
                semanticModel,
                compilation,
                mapperType,
                sourceParameter,
                sourceName,
                previousParameter,
                previousSubstitution,
                resultParameter,
                resultName,
                transferScope,
                BuildLeaf,
                cancellationToken,
                out var lowered))
        {
            root = null!;
            return false;
        }

        root = ConvertMemberControlFlow(lowered);
        return true;
    }

    public static bool TryBuild(
        DeclarativeControlFlowProgram program,
        SemanticModel semanticModel,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        IParameterSymbol? resultParameter,
        string? resultName,
        SyntaxNode transferScope,
        Func<DeclarativeLeafSyntaxNode, TypeMapperControlFlowNode?>
            buildLeaf,
        CancellationToken cancellationToken,
        out TypeMapperControlFlowNode root)
    {
        return TryBuild(
            program,
            semanticModel,
            compilation,
            mapperType,
            sourceParameter,
            sourceName,
            previousParameter,
            previousSubstitution,
            resultParameter,
            resultName,
            transferScope,
            buildLeaf,
            buildCondition: null,
            cancellationToken,
            out root);
    }

    public static bool TryBuild(
        DeclarativeControlFlowProgram program,
        SemanticModel semanticModel,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType,
        IParameterSymbol sourceParameter,
        string sourceName,
        IParameterSymbol? previousParameter,
        PreviousExpressionSubstitution? previousSubstitution,
        IParameterSymbol? resultParameter,
        string? resultName,
        SyntaxNode transferScope,
        Func<DeclarativeLeafSyntaxNode, TypeMapperControlFlowNode?>
            buildLeaf,
        Func<
            ExpressionSyntax,
            TypeMapperControlFlowNode,
            TypeMapperControlFlowNode,
            TypeMapperControlFlowNode?>? buildCondition,
        CancellationToken cancellationToken,
        out TypeMapperControlFlowNode root)
    {
        TypeMapperRewrittenDependencyExpression?
            RewriteDependency(ExpressionSyntax expression)
        {
            if (expression is IdentifierNameSyntax identifier &&
                program.RuntimeLocalPlaceholders.Values.Contains(
                    identifier.Identifier.ValueText,
                    StringComparer.Ordinal))
            {
                var symbol = program.RuntimeLocalPlaceholders.First(
                    pair => StringComparer.Ordinal.Equals(
                        pair.Value,
                        identifier.Identifier.ValueText)).Key;
                var type = symbol switch
                {
                    ILocalSymbol local => local.Type,
                    IParameterSymbol parameter => parameter.Type,
                    _ => null
                };

                if (DeclarativeNestedMapExpression
                    .TryGetMarkerDestinationType(
                        type,
                        out var nestedDestinationType))
                {
                    type = nestedDestinationType;
                }
                else if (DeclarativeNestedMapExpression
                         .IsMapMarkerType(type))
                {
                    type = null;
                }

                return type is null
                    ? null
                    : new TypeMapperRewrittenDependencyExpression(
                        identifier.Identifier.Text,
                        new TypeMapperDependencyExpressionModel(
                            new TypeMapperDependencyExpressionNodeModel(
                                DeclarativeDependencyExpressionBuilder
                                    .BuildDeclaredValueKey(
                                        symbol,
                                        sourceParameter,
                                        previousParameter,
                                        resultParameter),
                                TypeMapperMappingTypePolicy
                                    .GetGeneratedTypeName(type),
                                CanMaterialize: false,
                                identifier.Identifier.Text,
                                [])));
            }

            return DeclarativeDependencyExpressionBuilder.TryRewrite(
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
                    program.RuntimeLocalPlaceholders,
                    fallbackType: null,
                    cancellationToken,
                    out var rewritten,
                    out var dependency)
                ? new TypeMapperRewrittenDependencyExpression(
                    rewritten,
                    dependency)
                : null;
        }

        string? Rewrite(ExpressionSyntax expression) =>
            RewriteDependency(expression)?.Expression;

        string? RewritePattern(PatternSyntax pattern)
        {
            if (!ConstructExpressionRewriter.TryRewriteSyntax(
                    pattern,
                    semanticModel,
                    mapperType,
                    sourceParameter,
                    sourceName,
                    previousParameter,
                    previousSubstitution,
                    resultParameter,
                    resultName,
                    transferScope,
                    program.RuntimeLocalPlaceholders,
                    cancellationToken,
                    out PatternSyntax rewritten))
            {
                return null;
            }

            return rewritten
                .WithoutTrivia()
                .NormalizeWhitespace()
                .ToFullString();
        }

        string? RewriteLabel(DeclarativeSwitchLabelSyntax label)
        {
            switch (label.Kind)
            {
                case DeclarativeSwitchLabelKind.Default:
                    return "default:";

                case DeclarativeSwitchLabelKind.Value
                    when label.Value is { } value:
                {
                    var rewrittenValue = Rewrite(value);
                    return rewrittenValue is null
                        ? null
                        : "case " + rewrittenValue + ":";
                }

                case DeclarativeSwitchLabelKind.Pattern
                    when label.Pattern is { } pattern:
                {
                    var rewrittenPattern = RewritePattern(pattern);
                    var rewrittenCondition =
                        label.WhenCondition is { } condition
                            ? Rewrite(condition)
                            : null;

                    if (rewrittenPattern is null ||
                        label.WhenCondition is not null &&
                        rewrittenCondition is null)
                    {
                        return null;
                    }

                    return "case " + rewrittenPattern +
                           (rewrittenCondition is null
                               ? string.Empty
                               : " when " + rewrittenCondition) +
                           ":";
                }

                default:
                    return null;
            }
        }

        TypeMapperControlFlowNode? BuildNode(
            DeclarativeControlFlowSyntaxNode node)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (node is DeclarativeLeafSyntaxNode leaf)
            {
                return buildLeaf(leaf);
            }

            if (node is DeclarativeThrowSyntaxNode throwNode)
            {
                var throwExpression =
                    RewriteDependency(throwNode.Expression);

                return throwExpression is null
                    ? null
                    : new TypeMapperControlFlowNode(
                        Locals: [],
                        Condition: null,
                        WhenTrue: null,
                        WhenFalse: null,
                        Leaf: null,
                        throwExpression.Value.Expression,
                        ThrowDependency:
                            throwExpression.Value.DependencyExpression);
            }

            if (node is DeclarativeLocalDeclarationsSyntaxNode locals)
            {
                var next = BuildNode(locals.Next);

                if (next is null)
                {
                    return null;
                }

                var runtimeLocals =
                    ImmutableArray.CreateBuilder<
                        TypeMapperLocalValueModel>();

                foreach (var placeholder in
                         locals.RuntimeLocalPlaceholders)
                {
                    var local = program.RuntimeLocals.FirstOrDefault(
                        candidate => StringComparer.Ordinal.Equals(
                            candidate.PlaceholderName,
                            placeholder));

                    if (local.PlaceholderName is null)
                    {
                        return null;
                    }

                    var initializer =
                        RewriteDependency(local.Initializer);

                    if (initializer is null)
                    {
                        return null;
                    }

                    var declaredSymbol =
                        program.RuntimeLocalPlaceholders
                            .First(pair =>
                                StringComparer.Ordinal.Equals(
                                    pair.Value,
                                    placeholder))
                            .Key;
                    var declaredType = declaredSymbol switch
                    {
                        ILocalSymbol declaredLocal => declaredLocal.Type,
                        IParameterSymbol declaredParameter =>
                            declaredParameter.Type,
                        _ => null
                    };

                    if (declaredType is null)
                    {
                        return null;
                    }

                    ITypeSymbol storedType = declaredType;

                    if (DeclarativeNestedMapExpression
                        .TryGetMarkerDestinationType(
                            declaredType,
                            out var nestedDestinationType))
                    {
                        if (local.DeclarationType != "var")
                        {
                            return null;
                        }

                        storedType = nestedDestinationType;
                    }
                    else if (DeclarativeNestedMapExpression
                             .IsMapMarkerType(declaredType))
                    {
                        return null;
                    }

                    runtimeLocals.Add(
                        new TypeMapperLocalValueModel(
                            local.DeclarationType,
                            placeholder,
                            initializer.Value.Expression,
                            local.IsConst,
                            DependencyExpression:
                                initializer.Value.DependencyExpression,
                            DeclaredValueKey:
                                DeclarativeDependencyExpressionBuilder
                                    .BuildDeclaredValueKey(
                                        declaredSymbol,
                                        sourceParameter,
                                        previousParameter,
                                        resultParameter),
                            StoredValueTypeName:
                                TypeMapperMappingTypePolicy
                                    .GetGeneratedTypeName(storedType)));
                }

                return next with
                {
                    Locals = runtimeLocals.ToImmutable()
                        .AddRange(next.Locals)
                };
            }

            if (node is DeclarativeConditionalSyntaxNode conditional)
            {
                var whenTrue = BuildNode(conditional.WhenTrue);
                var whenFalse = BuildNode(conditional.WhenFalse);

                if (whenTrue is null ||
                    whenFalse is null)
                {
                    return null;
                }

                if (buildCondition is not null)
                {
                    return buildCondition(
                        conditional.Condition,
                        whenTrue,
                        whenFalse);
                }

                var condition =
                    RewriteDependency(conditional.Condition);

                if (condition is null)
                {
                    return null;
                }

                if (StringComparer.Ordinal.Equals(
                        condition.Value.Expression,
                        "true"))
                {
                    return whenTrue;
                }

                if (StringComparer.Ordinal.Equals(
                        condition.Value.Expression,
                        "false"))
                {
                    return whenFalse;
                }

                return Equals(whenTrue, whenFalse)
                    ? new TypeMapperControlFlowNode(
                        Locals: [],
                        Condition: null,
                        WhenTrue: null,
                        WhenFalse: null,
                        Leaf: null,
                        ThrowExpression: null,
                        EvaluationExpression:
                            condition.Value.Expression,
                        EvaluationContinuation: whenTrue,
                        EvaluationDependency:
                            condition.Value.DependencyExpression)
                    : new TypeMapperControlFlowNode(
                        Locals: [],
                        condition.Value.Expression,
                        whenTrue,
                        whenFalse,
                        Leaf: null,
                        ThrowExpression: null,
                        ConditionDependency:
                            condition.Value.DependencyExpression);
            }

            var switchNode = (DeclarativeSwitchSyntaxNode)node;
            var governingExpression = RewriteDependency(
                switchNode.GoverningExpression);

            if (governingExpression is null)
            {
                return null;
            }

            var sections =
                ImmutableArray.CreateBuilder<TypeMapperSwitchSectionModel>();

            foreach (var section in switchNode.Sections)
            {
                var labels =
                    ImmutableArray.CreateBuilder<string>();

                foreach (var label in section.Labels)
                {
                    var rewrittenLabel = RewriteLabel(label);

                    if (rewrittenLabel is null)
                    {
                        return null;
                    }

                    labels.Add(rewrittenLabel);
                }

                var branch = BuildNode(section.Branch);

                if (branch is null)
                {
                    return null;
                }

                sections.Add(
                    new TypeMapperSwitchSectionModel(
                        labels.ToImmutable(),
                        branch));
            }

            TypeMapperControlFlowNode? continuation = null;

            if (switchNode.Continuation is { } continuationSyntax)
            {
                continuation = BuildNode(continuationSyntax);

                if (continuation is null)
                {
                    return null;
                }
            }

            var result = new TypeMapperControlFlowNode(
                Locals: [],
                Condition: null,
                WhenTrue: null,
                WhenFalse: null,
                Leaf: null,
                ThrowExpression: null,
                governingExpression.Value.Expression,
                sections.ToImmutable(),
                continuation,
                switchNode.RequiresFallback,
                switchNode.CanPassUnmatchedValue,
                SwitchDependency:
                    governingExpression.Value.DependencyExpression);

            return switchNode.RequiresFallback
                ? MaterializeSwitchFallback(
                    result,
                    compilation,
                    mapperType)
                : result;
        }

        var lowered = BuildNode(program.Root);

        if (lowered is null)
        {
            root = null!;
            return false;
        }

        var requiredLocals = CollectRequiredLocals(
            lowered,
            program.RuntimeLocals);
        var pruned = PruneLocals(lowered, requiredLocals);
        var names = AllocateLocalNames(
            pruned,
            program,
            requiredLocals,
            mapperType);

        root = RenameControlFlow(pruned, names);
        return true;
    }

    private static TypeMapperControlFlowNode MaterializeSwitchFallback(
        TypeMapperControlFlowNode node,
        CSharpCompilation compilation,
        INamedTypeSymbol mapperType)
    {
        var usedNames = UserResultMappingPlanner.BuildUsedLocalNames(
            mapperType);
        CollectDeclaredNames(node, usedNames);
        var switchValueName = UserResultMappingPlanner.AllocateName(
            "switchValue",
            usedNames);
        var fallback = new TypeMapperControlFlowNode(
            Locals: [],
            Condition: null,
            WhenTrue: null,
            WhenFalse: null,
            Leaf: null,
            ThrowExpression: BuildUnmatchedSwitchException(
                switchValueName,
                node.SwitchCanPassUnmatchedValue,
                compilation));

        return node with
        {
            Locals = node.Locals.Add(
                new TypeMapperLocalValueModel(
                    "var",
                    switchValueName,
                    node.SwitchExpression!,
                    IsConst: false,
                    IsSynthetic: true,
                    DependencyExpression:
                        node.SwitchDependency,
                    StoredValueTypeName:
                        node.SwitchDependency?.Root.ValueTypeName)),
            SwitchExpression = switchValueName,
            SwitchContinuation = fallback,
            SwitchRequiresFallback = false
        };
    }

    private static TypeMapperMemberControlFlowNode
        ConvertMemberControlFlow(TypeMapperControlFlowNode node)
    {
        return new TypeMapperMemberControlFlowNode(
            node.Locals,
            node.Condition,
            node.WhenTrue is { } whenTrue
                ? ConvertMemberControlFlow(whenTrue)
                : null,
            node.WhenFalse is { } whenFalse
                ? ConvertMemberControlFlow(whenFalse)
                : null,
            node.Leaf?.MapExistingMemberMappings ?? [],
            node.ThrowExpression,
            node.Leaf?.UnsupportedExceptionMessage,
            node.SwitchExpression,
            node.SwitchSections.IsDefault
                ? default
                : node.SwitchSections.Select(section =>
                        new TypeMapperMemberSwitchSectionModel(
                            section.Labels,
                            ConvertMemberControlFlow(
                                section.Branch)))
                    .ToImmutableArray(),
            node.SwitchContinuation is { } continuation
                ? ConvertMemberControlFlow(continuation)
                : null,
            node.SwitchRequiresFallback,
            node.SwitchCanPassUnmatchedValue,
            node.EvaluationExpression,
            node.EvaluationContinuation is { } evaluationContinuation
                ? ConvertMemberControlFlow(evaluationContinuation)
                : null,
            node.ConditionDependency,
            node.ThrowDependency,
            node.SwitchDependency,
            node.EvaluationDependency);
    }

    private static string BuildUnmatchedSwitchException(
        string valueExpression,
        bool canPassUnmatchedValue,
        CSharpCompilation compilation)
    {
        const string metadataName =
            "System.Runtime.CompilerServices.SwitchExpressionException";
        var exceptionType = compilation.GetTypeByMetadataName(metadataName);

        if (exceptionType is not null &&
            canPassUnmatchedValue &&
            exceptionType.InstanceConstructors.Any(constructor =>
                constructor.DeclaredAccessibility == Accessibility.Public &&
                constructor.Parameters.Length == 1 &&
                constructor.Parameters[0].Type.SpecialType ==
                SpecialType.System_Object))
        {
            return "new global::" + metadataName +
                   $"({valueExpression})";
        }

        if (exceptionType is not null &&
            exceptionType.InstanceConstructors.Any(constructor =>
                constructor.DeclaredAccessibility == Accessibility.Public &&
                constructor.Parameters.IsEmpty))
        {
            return "new global::" + metadataName + "()";
        }

        return "new global::System.InvalidOperationException()";
    }

    private static HashSet<string> CollectRequiredLocals(
        TypeMapperControlFlowNode root,
        ImmutableArray<DeclarativeRuntimeLocalSyntax> locals)
    {
        var expressions = EnumerateExpressions(
                root,
                includeLocalInitializers: false)
            .ToArray();
        var required = new HashSet<string>(StringComparer.Ordinal);

        foreach (var local in locals)
        {
            if (expressions.Any(expression =>
                    ReferencesIdentifier(
                        expression,
                        local.PlaceholderName)))
            {
                required.Add(local.PlaceholderName);
            }
        }

        var changed = true;

        while (changed)
        {
            changed = false;

            foreach (var local in locals)
            {
                if (!required.Contains(local.PlaceholderName))
                {
                    continue;
                }

                var initializer = root
                    .DescendantLocal(local.PlaceholderName)
                    ?.ValueExpression;

                if (initializer is null)
                {
                    continue;
                }

                foreach (var dependency in locals)
                {
                    if (!required.Contains(dependency.PlaceholderName) &&
                        ReferencesIdentifier(
                            initializer,
                            dependency.PlaceholderName))
                    {
                        required.Add(dependency.PlaceholderName);
                        changed = true;
                    }
                }
            }
        }

        return required;
    }

    private static TypeMapperLocalValueModel? DescendantLocal(
        this TypeMapperControlFlowNode node,
        string name)
    {
        foreach (var local in node.Locals)
        {
            if (StringComparer.Ordinal.Equals(local.Name, name))
            {
                return local;
            }
        }

        if (node.EvaluationContinuation is { } evaluationContinuation &&
            evaluationContinuation.DescendantLocal(name) is { } evaluation)
        {
            return evaluation;
        }

        if (node.SwitchExpression is not null)
        {
            foreach (var section in node.SwitchSections)
            {
                if (section.Branch.DescendantLocal(name) is { } local)
                {
                    return local;
                }
            }

            return node.SwitchContinuation?.DescendantLocal(name);
        }

        return node.Condition is null
            ? null
            : node.WhenTrue?.DescendantLocal(name) ??
              node.WhenFalse?.DescendantLocal(name);
    }

    private static TypeMapperControlFlowNode PruneLocals(
        TypeMapperControlFlowNode node,
        HashSet<string> required)
    {
        var locals = node.Locals
            .Where(local =>
                local.IsSynthetic || required.Contains(local.Name))
            .ToImmutableArray();

        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            return node with
            {
                Locals = locals,
                EvaluationContinuation = PruneLocals(
                    evaluationContinuation,
                    required)
            };
        }

        if (node.SwitchExpression is not null)
        {
            return node with
            {
                Locals = locals,
                SwitchSections = node.SwitchSections.Select(section =>
                        section with
                        {
                            Branch = PruneLocals(
                                section.Branch,
                                required)
                        })
                    .ToImmutableArray(),
                SwitchContinuation = node.SwitchContinuation is
                    { } continuation
                    ? PruneLocals(continuation, required)
                    : null
            };
        }

        return node.Condition is null
            ? node with { Locals = locals }
            : node with
            {
                Locals = locals,
                WhenTrue = PruneLocals(node.WhenTrue!, required),
                WhenFalse = PruneLocals(node.WhenFalse!, required)
            };
    }

    private static IReadOnlyDictionary<string, string> AllocateLocalNames(
        TypeMapperControlFlowNode root,
        DeclarativeControlFlowProgram program,
        HashSet<string> requiredLocals,
        INamedTypeSymbol mapperType)
    {
        var usedNames = UserResultMappingPlanner.BuildUsedLocalNames(
            mapperType);
        CollectDeclaredNames(root, usedNames);

        foreach (var expression in EnumerateExpressions(
                     root,
                     includeLocalInitializers: true))
        {
            foreach (var token in SyntaxFactory.ParseTokens(expression))
            {
                if (token.IsKind(SyntaxKind.IdentifierToken) &&
                    !program.RuntimeLocals.Any(local =>
                        StringComparer.Ordinal.Equals(
                            local.PlaceholderName,
                            token.ValueText)) &&
                    !program.BoundLocals.Any(local =>
                        StringComparer.Ordinal.Equals(
                            local.PlaceholderName,
                            token.ValueText)))
                {
                    usedNames.Add(token.ValueText);
                }
            }
        }

        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var local in program.RuntimeLocals)
        {
            if (!requiredLocals.Contains(local.PlaceholderName))
            {
                continue;
            }

            names.Add(
                local.PlaceholderName,
                UserResultMappingPlanner.AllocateName(
                    local.PreferredName,
                    usedNames));
        }

        foreach (var local in program.BoundLocals)
        {
            names.Add(
                local.PlaceholderName,
                UserResultMappingPlanner.AllocateName(
                    local.PreferredName,
                    usedNames));
        }

        return names;
    }

    private static void CollectDeclaredNames(
        TypeMapperControlFlowNode node,
        HashSet<string> names)
    {
        foreach (var local in node.Locals)
        {
            if (local.IsSynthetic)
            {
                names.Add(local.Name);
            }
        }

        if (node.Leaf is { } leaf)
        {
            names.Add(leaf.NonNullSourceName);
            names.Add(leaf.ResultLocalName);

            if (leaf.MapNewFactory is { } factory)
            {
                names.Add(factory.DestinationLocalName);

                if (factory.NullableValueLocalName is { } valueLocal)
                {
                    names.Add(valueLocal);
                }
            }

            if (leaf.MapNewConstructor is { } constructor)
            {
                foreach (var argument in constructor.Arguments)
                {
                    if (argument.ValueLocalName is { } valueLocal)
                    {
                        names.Add(valueLocal);
                    }
                }
            }

            foreach (var member in leaf.MapNewMemberMappings
                         .AddRange(leaf.MapNewPostMemberMappings)
                         .AddRange(leaf.MapExistingMemberMappings))
            {
                if (member.SourceValueLocalName is { } sourceLocal)
                {
                    names.Add(sourceLocal);
                }

                if (member.ValueLocalName is { } valueLocal)
                {
                    names.Add(valueLocal);
                }
            }

            return;
        }

        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            CollectDeclaredNames(evaluationContinuation, names);
            return;
        }

        if (node.SwitchExpression is not null)
        {
            foreach (var section in node.SwitchSections)
            {
                CollectDeclaredNames(section.Branch, names);
            }

            if (node.SwitchContinuation is { } continuation)
            {
                CollectDeclaredNames(continuation, names);
            }

            return;
        }

        if (node.Condition is not null)
        {
            CollectDeclaredNames(node.WhenTrue!, names);
            CollectDeclaredNames(node.WhenFalse!, names);
        }
    }

    private static TypeMapperControlFlowNode RenameControlFlow(
        TypeMapperControlFlowNode node,
        IReadOnlyDictionary<string, string> names)
    {
        var locals = node.Locals.Select(local =>
                local with
                {
                    Name = local.IsSynthetic
                        ? local.Name
                        : names[local.Name],
                    ValueExpression = RenameTokens(
                        local.ValueExpression,
                        names),
                    DependencyExpression = RenameDependencyExpression(
                        local.DependencyExpression,
                        names)
                })
            .ToImmutableArray();

        if (node.EvaluationContinuation is { } evaluationContinuation)
        {
            return node with
            {
                Locals = locals,
                EvaluationExpression = RenameTokens(
                    node.EvaluationExpression!,
                    names),
                EvaluationDependency = RenameDependencyExpression(
                    node.EvaluationDependency,
                    names),
                EvaluationContinuation = RenameControlFlow(
                    evaluationContinuation,
                    names)
            };
        }

        if (node.SwitchExpression is not null)
        {
            return node with
            {
                Locals = locals,
                SwitchExpression = RenameTokens(
                    node.SwitchExpression,
                    names),
                SwitchDependency = RenameDependencyExpression(
                    node.SwitchDependency,
                    names),
                SwitchSections = node.SwitchSections.Select(section =>
                        section with
                        {
                            Labels = section.Labels.Select(label =>
                                    RenameTokens(label, names))
                                .ToImmutableArray(),
                            Branch = RenameControlFlow(
                                section.Branch,
                                names)
                        })
                    .ToImmutableArray(),
                SwitchContinuation = node.SwitchContinuation is
                    { } continuation
                    ? RenameControlFlow(continuation, names)
                    : null
            };
        }

        if (node.Condition is not null)
        {
            return node with
            {
                Locals = locals,
                Condition = RenameTokens(node.Condition, names),
                ConditionDependency = RenameDependencyExpression(
                    node.ConditionDependency,
                    names),
                WhenTrue = RenameControlFlow(node.WhenTrue!, names),
                WhenFalse = RenameControlFlow(node.WhenFalse!, names)
            };
        }

        return node with
        {
            Locals = locals,
            ThrowExpression = node.ThrowExpression is { } throwExpression
                ? RenameTokens(throwExpression, names)
                : null,
            ThrowDependency = RenameDependencyExpression(
                node.ThrowDependency,
                names),
            Leaf = node.Leaf is { } leaf
                ? RenameMapping(leaf, names)
                : null
        };
    }

    private static TypeMapperMappingModel RenameMapping(
        TypeMapperMappingModel mapping,
        IReadOnlyDictionary<string, string> names)
    {
        TypeMapperMemberMappingModel RenameMember(
            TypeMapperMemberMappingModel member) =>
            member with
            {
                DependencyExpression = RenameDependencyExpression(
                    member.DependencyExpression,
                    names),
                ExplicitValueExpression =
                    member.DependencyExpression is { } dependency
                        ? RenameDependencyExpression(
                                dependency,
                                names)!
                            .Render()
                        : member.ExplicitValueExpression is
                            { } expression
                            ? RenameTokens(expression, names)
                            : null
            };

        return mapping with
        {
            MapNewDirectExpression = mapping.MapNewDirectExpression is
                { } mapNewDirect
                ? RenameTokens(mapNewDirect, names)
                : null,
            MapExistingDirectExpression =
                mapping.MapExistingDirectExpression is
                    { } mapExistingDirect
                    ? RenameTokens(mapExistingDirect, names)
                    : null,
            MapNewFactory = mapping.MapNewFactory is { } factory
                ? factory with
                {
                    ValueExpression = RenameTokens(
                        factory.ValueExpression,
                        names)
                }
                : null,
            MapNewConstructor = mapping.MapNewConstructor is
                { } constructor
                ? constructor with
                {
                    Arguments = constructor.Arguments.Select(argument =>
                            argument with
                            {
                                DependencyExpression =
                                    RenameDependencyExpression(
                                        argument.DependencyExpression,
                                        names),
                                ExplicitValueExpression =
                                    argument.DependencyExpression is
                                        { } dependency
                                        ? RenameDependencyExpression(
                                                dependency,
                                                names)!
                                            .Render()
                                        : argument.ExplicitValueExpression is
                                            { } expression
                                            ? RenameTokens(expression, names)
                                            : null
                            })
                        .ToImmutableArray()
                }
                : null,
            MapNewMemberMappings = mapping.MapNewMemberMappings
                .Select(RenameMember)
                .ToImmutableArray(),
            MapNewPostMemberMappings = mapping.MapNewPostMemberMappings
                .Select(RenameMember)
                .ToImmutableArray(),
            MapExistingMemberMappings = mapping.MapExistingMemberMappings
                .Select(RenameMember)
                .ToImmutableArray()
        };
    }

    private static TypeMapperDependencyExpressionModel?
        RenameDependencyExpression(
            TypeMapperDependencyExpressionModel? expression,
            IReadOnlyDictionary<string, string> names)
    {
        if (expression is null)
        {
            return null;
        }

        TypeMapperDependencyExpressionNodeModel RenameNode(
            TypeMapperDependencyExpressionNodeModel node) =>
            node with
            {
                Template = RenameTokens(node.Template, names),
                Children = node.Children.Select(child =>
                        child with
                        {
                            Node = RenameNode(child.Node)
                        })
                    .ToImmutableArray()
            };

        return new TypeMapperDependencyExpressionModel(
            RenameNode(expression.Root));
    }

    private static IEnumerable<string> EnumerateExpressions(
        TypeMapperControlFlowNode node,
        bool includeLocalInitializers)
    {
        if (includeLocalInitializers)
        {
            foreach (var local in node.Locals)
            {
                yield return local.ValueExpression;
            }
        }

        if (node.EvaluationExpression is { } evaluationExpression)
        {
            yield return evaluationExpression;

            foreach (var expression in EnumerateExpressions(
                         node.EvaluationContinuation!,
                         includeLocalInitializers))
            {
                yield return expression;
            }

            yield break;
        }

        if (node.SwitchExpression is { } switchExpression)
        {
            yield return switchExpression;

            foreach (var section in node.SwitchSections)
            {
                foreach (var label in section.Labels)
                {
                    yield return label;
                }

                foreach (var expression in EnumerateExpressions(
                             section.Branch,
                             includeLocalInitializers))
                {
                    yield return expression;
                }
            }

            if (node.SwitchContinuation is { } continuation)
            {
                foreach (var expression in EnumerateExpressions(
                             continuation,
                             includeLocalInitializers))
                {
                    yield return expression;
                }
            }

            yield break;
        }

        if (node.Condition is { } condition)
        {
            yield return condition;

            foreach (var expression in EnumerateExpressions(
                         node.WhenTrue!,
                         includeLocalInitializers))
            {
                yield return expression;
            }

            foreach (var expression in EnumerateExpressions(
                         node.WhenFalse!,
                         includeLocalInitializers))
            {
                yield return expression;
            }

            yield break;
        }

        if (node.ThrowExpression is { } throwExpression)
        {
            yield return throwExpression;
            yield break;
        }

        if (node.Leaf is not { } leaf)
        {
            yield break;
        }

        if (leaf.MapNewDirectExpression is { } mapNewDirect)
        {
            yield return mapNewDirect;
        }

        if (leaf.MapExistingDirectExpression is { } mapExistingDirect)
        {
            yield return mapExistingDirect;
        }

        if (leaf.MapNewFactory is { } factory)
        {
            yield return factory.ValueExpression;
        }

        if (leaf.MapNewConstructor is { } constructor)
        {
            foreach (var argument in constructor.Arguments)
            {
                if (argument.ExplicitValueExpression is { } expression)
                {
                    yield return expression;
                }
            }
        }

        foreach (var member in leaf.MapNewMemberMappings
                     .AddRange(leaf.MapNewPostMemberMappings)
                     .AddRange(leaf.MapExistingMemberMappings))
        {
            if (member.ExplicitValueExpression is { } expression)
            {
                yield return expression;
            }
        }
    }

    private static bool ReferencesIdentifier(
        string expression,
        string identifier)
    {
        return SyntaxFactory.ParseTokens(expression)
            .Any(token =>
                token.IsKind(SyntaxKind.IdentifierToken) &&
                StringComparer.Ordinal.Equals(
                    token.ValueText,
                    identifier));
    }

    private static string RenameTokens(
        string syntax,
        IReadOnlyDictionary<string, string> names)
    {
        if (names.Count == 0)
        {
            return syntax;
        }

        var builder = new System.Text.StringBuilder();

        foreach (var token in SyntaxFactory.ParseTokens(syntax))
        {
            if (!token.IsKind(SyntaxKind.IdentifierToken) ||
                !names.TryGetValue(token.ValueText, out var replacement))
            {
                builder.Append(token.ToFullString());
                continue;
            }

            var valueText = replacement.StartsWith(
                "@",
                StringComparison.Ordinal)
                ? replacement.Substring(1)
                : replacement;
            builder.Append(
                SyntaxFactory.Identifier(
                        token.LeadingTrivia,
                        SyntaxKind.IdentifierToken,
                        replacement,
                        valueText,
                        token.TrailingTrivia)
                    .ToFullString());
        }

        return builder.ToString();
    }
}
