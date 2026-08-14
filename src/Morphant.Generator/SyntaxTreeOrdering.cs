using Microsoft.CodeAnalysis;

namespace Morphant.Generator;

internal sealed class SyntaxTreeOrdering
{
    private readonly Dictionary<SyntaxTree, int> _indices;

    public SyntaxTreeOrdering(IEnumerable<SyntaxTree> syntaxTrees)
    {
        _indices = syntaxTrees
            .Select((tree, index) => (tree, index))
            .ToDictionary(
                static item => item.tree,
                static item => item.index);
    }

    public bool Contains(SyntaxTree syntaxTree) =>
        _indices.ContainsKey(syntaxTree);

    public int GetOrder(SyntaxTree syntaxTree) =>
        _indices[syntaxTree];

    public int GetOrderOrDefault(SyntaxTree? syntaxTree) =>
        syntaxTree is not null &&
        _indices.TryGetValue(syntaxTree, out var index)
            ? index
            : int.MaxValue;
}
