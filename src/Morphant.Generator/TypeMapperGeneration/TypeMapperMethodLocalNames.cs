using System.Collections.Immutable;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class TypeMapperMethodLocalNames
{
    public static GeneratedLocalNameAllocator Build(
        TypeMapperMappingModel mapping,
        bool create)
    {
        var localNames = new GeneratedLocalNameAllocator(
            mapping.AnalysisContext.TargetMapper,
            "source",
            "destination",
            "context",
            "operation");

        localNames.Reserve(mapping.NonNullSourceName);

        if (mapping.ControlFlow is { } controlFlow)
        {
            ReserveControlFlow(
                create
                    ? controlFlow.CreateRoot
                    : controlFlow.UpdateRoot,
                create,
                localNames);
        }
        else
        {
            ReserveLeaf(
                mapping,
                create,
                allowReplacement: false,
                localNames);
        }

        return localNames;
    }

    private static void ReserveControlFlow(
        TypeMapperControlFlowNode node,
        bool create,
        GeneratedLocalNameAllocator localNames)
    {
        ReserveLocals(node.Locals, localNames);
        localNames.ReserveExpressionDeclarations(node.Condition);
        localNames.ReserveExpressionDeclarations(node.SwitchExpression);
        localNames.ReserveExpressionDeclarations(
            node.EvaluationExpression);
        localNames.ReserveExpressionDeclarations(
            node.EvaluationCondition);
        ReserveLocals(node.EvaluationLocals, localNames);
        localNames.ReserveExpressionDeclarations(node.ThrowExpression);

        foreach (var section in Normalize(node.SwitchSections))
        {
            foreach (var label in section.Labels)
            {
                localNames.ReserveSwitchLabelDeclarations(label);
            }
        }

        if (node.Leaf is { } leaf)
        {
            ReserveLeaf(
                leaf,
                create,
                allowReplacement: !create,
                localNames);
            return;
        }

        if (node.EvaluationContinuation is { } evaluation)
        {
            ReserveControlFlow(evaluation, create, localNames);
        }

        foreach (var section in Normalize(node.SwitchSections))
        {
            ReserveControlFlow(section.Branch, create, localNames);
        }

        if (node.SwitchContinuation is { } continuation)
        {
            ReserveControlFlow(continuation, create, localNames);
        }

        if (node.WhenTrue is { } whenTrue)
        {
            ReserveControlFlow(whenTrue, create, localNames);
        }

        if (node.WhenFalse is { } whenFalse)
        {
            ReserveControlFlow(whenFalse, create, localNames);
        }
    }

    private static void ReserveLeaf(
        TypeMapperMappingModel mapping,
        bool create,
        bool allowReplacement,
        GeneratedLocalNameAllocator localNames)
    {
        localNames.Reserve(mapping.NonNullSourceName);

        if (create)
        {
            ReserveCreateLeaf(mapping, localNames);
            return;
        }

        if (mapping.UpdateDirectExpression is { } directExpression)
        {
            localNames.ReserveExpressionDeclarations(directExpression);
            return;
        }

        if (allowReplacement &&
            (mapping.CreateFactory is not null ||
             mapping.CreateConstructor is not null))
        {
            ReserveCreateLeaf(mapping, localNames);
            return;
        }

        if (mapping.PostMemberControlFlow is { } postControlFlow)
        {
            ReserveMemberControlFlow(postControlFlow, localNames);
            return;
        }

        ReserveMembers(mapping.UpdateMemberMappings, localNames);
    }

    private static void ReserveCreateLeaf(
        TypeMapperMappingModel mapping,
        GeneratedLocalNameAllocator localNames)
    {
        if (mapping.CreateDirectExpression is { } directExpression)
        {
            localNames.ReserveExpressionDeclarations(directExpression);
            return;
        }

        if (mapping.CreateFactory is { } factory)
        {
            localNames.Reserve(factory.DestinationLocalName);
            localNames.Reserve(factory.NullableValueLocalName);
            localNames.ReserveExpressionDeclarations(
                factory.ValueExpression);
            ReservePostMembers(mapping, localNames);
            return;
        }

        if (mapping.CreateConstructor is not { } constructor)
        {
            return;
        }

        ReserveLocals(constructor.ValueLocals, localNames);

        foreach (var argument in constructor.Arguments)
        {
            localNames.Reserve(argument.ValueLocalName);
            localNames.ReserveExpressionDeclarations(
                argument.ExplicitValueExpression);
            ReserveLocals(argument.EvaluationLocals, localNames);
        }

        ReserveMembers(mapping.CreateMemberMappings, localNames);

        if (!mapping.CreatePostMemberMappings.IsEmpty ||
            mapping.PostMemberControlFlow is not null ||
            mapping.CreateTupleReconstruction is not null)
        {
            localNames.Reserve(mapping.ResultLocalName);
        }

        ReservePostMembers(mapping, localNames);
    }

    private static void ReservePostMembers(
        TypeMapperMappingModel mapping,
        GeneratedLocalNameAllocator localNames)
    {
        if (mapping.PostMemberControlFlow is { } postControlFlow)
        {
            ReserveMemberControlFlow(postControlFlow, localNames);
            return;
        }

        ReserveMembers(mapping.CreatePostMemberMappings, localNames);
    }

    private static void ReserveMemberControlFlow(
        TypeMapperMemberControlFlowNode node,
        GeneratedLocalNameAllocator localNames)
    {
        ReserveLocals(node.Locals, localNames);
        ReserveMembers(node.MemberMappings, localNames);
        localNames.ReserveExpressionDeclarations(node.Condition);
        localNames.ReserveExpressionDeclarations(node.SwitchExpression);
        localNames.ReserveExpressionDeclarations(
            node.EvaluationExpression);
        localNames.ReserveExpressionDeclarations(
            node.EvaluationCondition);
        ReserveLocals(node.EvaluationLocals, localNames);
        localNames.ReserveExpressionDeclarations(node.ThrowExpression);

        foreach (var section in Normalize(node.SwitchSections))
        {
            foreach (var label in section.Labels)
            {
                localNames.ReserveSwitchLabelDeclarations(label);
            }
        }

        if (node.EvaluationContinuation is { } evaluation)
        {
            ReserveMemberControlFlow(evaluation, localNames);
        }

        foreach (var section in Normalize(node.SwitchSections))
        {
            ReserveMemberControlFlow(section.Branch, localNames);
        }

        if (node.SwitchContinuation is { } continuation)
        {
            ReserveMemberControlFlow(continuation, localNames);
        }

        if (node.WhenTrue is { } whenTrue)
        {
            ReserveMemberControlFlow(whenTrue, localNames);
        }

        if (node.WhenFalse is { } whenFalse)
        {
            ReserveMemberControlFlow(whenFalse, localNames);
        }
    }

    private static void ReserveMembers(
        ImmutableArray<TypeMapperMemberMappingModel> mappings,
        GeneratedLocalNameAllocator localNames)
    {
        foreach (var mapping in Normalize(mappings))
        {
            localNames.Reserve(mapping.SourceValueLocalName);
            localNames.Reserve(mapping.ValueLocalName);
            localNames.ReserveExpressionDeclarations(
                mapping.ExplicitValueExpression);
            ReserveLocals(mapping.EvaluationLocals, localNames);
            ReserveLocals(
                mapping.InvocationArgumentLocals,
                localNames);
        }
    }

    private static void ReserveLocals(
        ImmutableArray<TypeMapperLocalValueModel> locals,
        GeneratedLocalNameAllocator localNames)
    {
        foreach (var local in Normalize(locals))
        {
            localNames.Reserve(local.Name);
            localNames.ReserveExpressionDeclarations(
                local.ValueExpression);
        }
    }

    private static ImmutableArray<T> Normalize<T>(
        ImmutableArray<T> values) =>
        values.IsDefault
            ? ImmutableArray<T>.Empty
            : values;
}
