using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.TypeMapperGeneration;

// One analysis of one immutable mapper version. A changed mapping starts a
// new probe; unchanged passes keep the already emitted and bound syntax.
internal sealed class TypeMapperProbe
{
    private readonly CSharpCompilation _compilation;
    private readonly CSharpParseOptions? _parseOptions;
    private readonly CancellationToken _cancellationToken;
    private GeneratedMapperSyntax? _syntax;
    private Dictionary<string, MethodDeclarationSyntax>? _methods;
    private ImmutableArray<Diagnostic> _diagnostics;

    public TypeMapperProbe(TypeMapperModel model, CSharpCompilation compilation,
        CSharpParseOptions? parseOptions, CancellationToken cancellationToken)
    {
        Model = model;
        _compilation = compilation;
        _parseOptions = parseOptions;
        _cancellationToken = cancellationToken;
    }

    public TypeMapperModel Model { get; }

    private GeneratedMapperSyntax Syntax => _syntax ??= CollectionCallerInformation.Restore(
        GeneratedMapperSyntax.Parse(TypeMapperEmitter.EmitTransferProbe(Model).ToString(),
            _compilation, _parseOptions, _cancellationToken, "Morphant.TransferProbe.g.cs"));

    public SourceText Source => Syntax.Text;
    public SyntaxTree Tree => Syntax.Tree;
    public SyntaxNode Root => Syntax.Root;
    public SemanticModel SemanticModel => Syntax.SemanticModel;

    public IReadOnlyDictionary<string, MethodDeclarationSyntax> Methods => _methods ??=
        Root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => method.ExplicitInterfaceSpecifier is null)
            .ToDictionary(method => method.Identifier.ValueText, StringComparer.Ordinal);

    public ImmutableArray<Diagnostic> Diagnostics
    {
        get
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (_diagnostics.IsDefault)
                _diagnostics = SemanticModel.GetDiagnostics(cancellationToken: _cancellationToken);
            return _diagnostics;
        }
    }

    public TypeMapperProbe WithMappings(ImmutableArray<TypeMapperMappingModel> mappings) =>
        Model.Mappings.SequenceEqual(mappings)
            ? this
            : new TypeMapperProbe(Model with { Mappings = mappings },
                _compilation, _parseOptions, _cancellationToken);

    public TypeMapperProbe WithValidatedMappings(ImmutableArray<TypeMapperMappingModel> mappings)
    {
        var candidate = WithMappings(mappings);
        if (ReferenceEquals(candidate, this)) return this;

        // Moving code into a helper or lambda can lose nullable flow or ref
        // safety. Revert only mappings that gain warnings or errors.
        var originalDiagnostics = GetMappingDiagnostics()
            .GroupBy(diagnostic => diagnostic)
            .ToDictionary(group => group.Key, group => group.Count());
        var rejected = new HashSet<int>();
        foreach (var group in candidate.GetMappingDiagnostics().GroupBy(diagnostic => diagnostic))
        {
            if (!originalDiagnostics.TryGetValue(group.Key, out var count) || group.Count() > count)
                rejected.Add(group.Key.Index);
        }

        if (rejected.Count == 0) return candidate;
        var accepted = mappings.ToBuilder();
        foreach (var index in rejected)
            accepted[index] = Model.Mappings[index];
        return WithMappings(accepted.ToImmutable());
    }

    private IEnumerable<(int Index, string Key)> GetMappingDiagnostics() => Diagnostics
        .Where(diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
        .Select(diagnostic => TypeMapperEmitter.TryGetTransferProbeMappingIndex(diagnostic, out var index)
            ? (Index: index, Key: diagnostic.Id + ":" + diagnostic.GetMessage())
            : (Index: -1, Key: string.Empty))
        .Where(diagnostic => diagnostic.Index >= 0);
}
