using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.TypeMapperGeneration;

// An immutable generated file version. Unchanged passes share its lazy analysis;
// a text or syntax edit always gets a new semantic model from the same compilation.
internal sealed class GeneratedMapperSyntax
{
    private SourceText? _text;
    private CompilationUnitSyntax? _root;
    private SemanticModel? _semanticModel;

    private GeneratedMapperSyntax(SyntaxTree tree, CSharpCompilation compilation, CancellationToken cancellationToken)
    {
        Tree = tree;
        Compilation = compilation;
        CancellationToken = cancellationToken;
    }

    public SyntaxTree Tree { get; }
    public CSharpCompilation Compilation { get; }
    public CancellationToken CancellationToken { get; }
    public SourceText Text => _text ??= Tree.GetText(CancellationToken);
    public CompilationUnitSyntax Root => _root ??= Tree.GetCompilationUnitRoot(CancellationToken);
    public SemanticModel SemanticModel => _semanticModel ??= Compilation.AddSyntaxTrees(Tree).GetSemanticModel(Tree);

    public static GeneratedMapperSyntax Parse(string source, CSharpCompilation compilation,
        CSharpParseOptions? options, CancellationToken cancellationToken, string path = "") =>
        new(CSharpSyntaxTree.ParseText(SourceText.From(source, System.Text.Encoding.UTF8),
            options, path, cancellationToken), compilation, cancellationToken);

    public GeneratedMapperSyntax WithRoot(CompilationUnitSyntax root) => ReferenceEquals(root, Root)
        ? this
        : new GeneratedMapperSyntax(CSharpSyntaxTree.Create(root, (CSharpParseOptions)Tree.Options,
            Tree.FilePath, Tree.Encoding), Compilation, CancellationToken);

    public GeneratedMapperSyntax WithChanges(IReadOnlyCollection<TextChange> changes) => changes.Count == 0
        ? this
        : new GeneratedMapperSyntax(Tree.WithChangedText(Text.WithChanges(changes)), Compilation, CancellationToken);

    public bool Contains(string value) => Text.ToString().IndexOf(value, StringComparison.Ordinal) >= 0;
    public override string ToString() => Text.ToString();
}
