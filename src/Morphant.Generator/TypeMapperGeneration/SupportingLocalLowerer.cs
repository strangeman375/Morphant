using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TypeMapperGeneration;

internal static class SupportingLocalLowerer
{
    public static TypeMapperModel Lower(TypeMapperModel model, CSharpCompilation compilation,
        CSharpParseOptions? options, CancellationToken cancellationToken)
    {
        var tree = CSharpSyntaxTree.ParseText(TypeMapperEmitter.EmitTransferProbe(model), options,
            cancellationToken: cancellationToken);
        var semantic = compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);
        var methods = tree.GetRoot(cancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => method.ExplicitInterfaceSpecifier is null && method.Body is not null)
            .ToDictionary(method => method.Identifier.ValueText, StringComparer.Ordinal);
        HashSet<string> Find(string? name) => name is not null && methods.TryGetValue(name, out var method)
            ? FindSafeCopies(method, semantic, cancellationToken) : new HashSet<string>(StringComparer.Ordinal);

        return model with
        {
            Mappings = model.Mappings.Select(mapping =>
            {
                var create = Find(mapping.CreateImplMethodName);
                var update = Find(mapping.UpdateImplMethodName);
                if (mapping.ControlFlow is { } flow)
                    return mapping with { ControlFlow = new(
                        RewriteNode(flow.CreateRoot, create), RewriteNode(flow.UpdateRoot, update)) };
                var common = new HashSet<string>(create, StringComparer.Ordinal);
                common.IntersectWith(update);
                return mapping with
                {
                    CreatePostMemberMappings = RewriteMembers(mapping.CreatePostMemberMappings, create),
                    UpdateMemberMappings = RewriteMembers(mapping.UpdateMemberMappings, update),
                    PostMemberControlFlow = RewriteMemberNode(mapping.PostMemberControlFlow, common)
                };
            }).ToImmutableArray()
        };
    }

    private static HashSet<string> FindSafeCopies(MethodDeclarationSyntax method,
        SemanticModel semantic, CancellationToken cancellationToken)
    {
        var identifiers = method.Body!.DescendantNodes().OfType<IdentifierNameSyntax>().ToArray();
        var dataFlow = semantic.AnalyzeDataFlow(method.Body);
        bool Safe(VariableDeclaratorSyntax declaration)
        {
            if (declaration.Parent?.Parent is not LocalDeclarationStatementSyntax { UsingKeyword.RawKind: 0 } ||
                declaration.Initializer?.Value is not IdentifierNameSyntax original ||
                semantic.GetDeclaredSymbol(declaration, cancellationToken) is not ILocalSymbol copy ||
                semantic.GetSymbolInfo(original, cancellationToken).Symbol is not ILocalSymbol source ||
                copy.RefKind != RefKind.None || source.RefKind != RefKind.None ||
                !SymbolEqualityComparer.IncludeNullability.Equals(copy.Type,
                    source.Type.WithNullableAnnotation(copy.Type.NullableAnnotation)) ||
                copy.NullableAnnotation == NullableAnnotation.NotAnnotated &&
                    source.NullableAnnotation == NullableAnnotation.Annotated &&
                    semantic.GetTypeInfo(original, cancellationToken).Nullability.FlowState != NullableFlowState.NotNull ||
                !semantic.GetConversion(original, cancellationToken).IsIdentity ||
                dataFlow is not { Succeeded: true } ||
                dataFlow.Captured.Any(symbol => SymbolEqualityComparer.Default.Equals(symbol, source)) ||
                dataFlow.UnsafeAddressTaken.Any(symbol => SymbolEqualityComparer.Default.Equals(symbol, source)))
                return false;

            foreach (var identifier in identifiers)
            {
                var symbol = semantic.GetSymbolInfo(identifier, cancellationToken).Symbol;
                if (SymbolEqualityComparer.Default.Equals(symbol, copy))
                {
                    // Only the generated final writes may consume this temporary.
                    // This also excludes nameof, ref arguments and caller-expression text.
                    if (identifier.Parent is not AssignmentExpressionSyntax assignment ||
                        assignment.Right != identifier || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression) ||
                        semantic.GetSymbolInfo(assignment.Left, cancellationToken).Symbol is not (IPropertySymbol or IFieldSymbol) ||
                        !semantic.GetConversion(identifier, cancellationToken).IsIdentity)
                        return false;
                }
                else if (SymbolEqualityComparer.Default.Equals(symbol, source))
                {
                    foreach (var ancestor in identifier.Ancestors().TakeWhile(node => node != method))
                    {
                        if (ancestor is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or RefExpressionSyntax ||
                            ancestor is AssignmentExpressionSyntax write && write.Left.Span.Contains(identifier.Span) ||
                            ancestor is ArgumentSyntax argument && argument.RefKindKeyword.RawKind != 0 ||
                            ancestor is PrefixUnaryExpressionSyntax prefix &&
                                prefix.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression or SyntaxKind.AddressOfExpression ||
                            ancestor is PostfixUnaryExpressionSyntax postfix &&
                                postfix.Kind() is SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression ||
                            source.Type.IsValueType && ancestor is MemberAccessExpressionSyntax access &&
                                access.Expression.Span.Contains(identifier.Span))
                            return false;
                    }
                }
            }
            return true;
        }

        // A generated name may occur in independent branch scopes. Every
        // declaration of that name must be safe before changing the model.
        return new HashSet<string>(method.Body.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .GroupBy(variable => variable.Identifier.ValueText, StringComparer.Ordinal)
            .Where(group => group.All(Safe)).Select(group => group.Key), StringComparer.Ordinal);
    }

    private static ImmutableArray<TypeMapperMemberMappingModel> RewriteMembers(
        ImmutableArray<TypeMapperMemberMappingModel> members, HashSet<string> safe) =>
        Normalize(members).Select(member => member.ValueLocalName is { } name && safe.Contains(name)
            ? member with { ValueLocalName = null } : member).ToImmutableArray();

    private static TypeMapperMemberControlFlowNode? RewriteMemberNode(
        TypeMapperMemberControlFlowNode? node, HashSet<string> safe) => node is null ? null : node with
        {
            MemberMappings = RewriteMembers(node.MemberMappings, safe),
            WhenTrue = RewriteMemberNode(node.WhenTrue, safe),
            WhenFalse = RewriteMemberNode(node.WhenFalse, safe),
            EvaluationContinuation = RewriteMemberNode(node.EvaluationContinuation, safe),
            SwitchContinuation = RewriteMemberNode(node.SwitchContinuation, safe),
            SwitchSections = Normalize(node.SwitchSections).Select(section => section with
                { Branch = RewriteMemberNode(section.Branch, safe)! }).ToImmutableArray()
        };

    private static TypeMapperControlFlowNode RewriteNode(TypeMapperControlFlowNode node, HashSet<string> safe) =>
        node with
        {
            Leaf = node.Leaf is { } leaf ? leaf with
            {
                CreatePostMemberMappings = RewriteMembers(leaf.CreatePostMemberMappings, safe),
                UpdateMemberMappings = RewriteMembers(leaf.UpdateMemberMappings, safe),
                PostMemberControlFlow = RewriteMemberNode(leaf.PostMemberControlFlow, safe)
            } : null,
            WhenTrue = node.WhenTrue is { } yes ? RewriteNode(yes, safe) : null,
            WhenFalse = node.WhenFalse is { } no ? RewriteNode(no, safe) : null,
            EvaluationContinuation = node.EvaluationContinuation is { } evaluation ? RewriteNode(evaluation, safe) : null,
            SwitchContinuation = node.SwitchContinuation is { } continuation ? RewriteNode(continuation, safe) : null,
            SwitchSections = Normalize(node.SwitchSections).Select(section => section with
                { Branch = RewriteNode(section.Branch, safe) }).ToImmutableArray()
        };

    private static ImmutableArray<T> Normalize<T>(ImmutableArray<T> values) =>
        values.IsDefault ? ImmutableArray<T>.Empty : values;
}
