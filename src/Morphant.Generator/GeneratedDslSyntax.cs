using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal static class GeneratedDslSyntax
{
    private static readonly SyntaxAnnotation Annotation = new("Morphant.Generated.Dsl");

    public static SyntaxTree Mark(SyntaxTree tree, CancellationToken cancellationToken) =>
        tree.WithRootAndOptions(
            tree.GetRoot(cancellationToken).WithAdditionalAnnotations(Annotation), tree.Options);

    public static bool IsDeclaredType(ITypeSymbol type) =>
        type.DeclaringSyntaxReferences.Any(reference =>
            reference.SyntaxTree.GetRoot().HasAnnotation(Annotation));
}
