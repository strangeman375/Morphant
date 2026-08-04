using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Morphant.Generator.TemplateSurface.TemplateType;

internal static class TemplateTypeModelPipeline
{
    public static IncrementalValuesProvider<TemplateTypeModelResult> Build(
        IncrementalValuesProvider<TemplateTypeGenerationInput> generationInputs,
        IncrementalValueProvider<CompilationContext> compilationContext)
    {
        // CompilationProvider changes after every source edit. Reduce that
        // broad input to the declarations and references that can affect this
        // particular destination before building its model.
        var modelInputs = generationInputs
            .Combine(compilationContext)
            .Select(static (source, cancellationToken) =>
            {
                var (generationInput, context) = source;

                return TryBuildInput(
                    generationInput,
                    context,
                    cancellationToken);
            })
            .WhereHasValue()
            .WithComparer(TemplateTypeModelInputComparer.Instance);

        return modelInputs
            .Select(static (input, cancellationToken) =>
                BuildModel(input, cancellationToken))
            .WithComparer(TemplateTypeModelResultComparer.Instance)
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildTemplateTypeModels);
    }

    private static TemplateTypeModelInput? TryBuildInput(
        TemplateTypeGenerationInput generationInput,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var destinationType = context.Compilation.GetTypeByMetadataName(
            generationInput.Definition.MetadataName);

        if (destinationType is null)
        {
            return null;
        }

        var hintName = GeneratedSourceHintName.Create(
            "TemplateType",
            generationInput.HintNamePart);

        return new TemplateTypeModelInput(
            generationInput,
            hintName,
            destinationType,
            context,
            BuildDependencies(
                destinationType,
                context.Compilation,
                cancellationToken),
            context.Compilation.Assembly.Identity.ToString(),
            context.Compilation.Options.NullableContextOptions,
            context.Compilation.Options.MetadataImportOptions);
    }

    private static TemplateTypeModelResult BuildModel(
        TemplateTypeModelInput input,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var model = TemplateTypeModelBuilder.Build(
            input.DestinationType,
            input.GenerationInput.Definition,
            input.Context.Compilation,
            cancellationToken);

        return new TemplateTypeModelResult(
            input.HintName,
            model);
    }

    private static ImmutableArray<TemplateTypeModelDependency>
        BuildDependencies(
            INamedTypeSymbol destinationType,
            CSharpCompilation compilation,
            CancellationToken cancellationToken)
    {
        var result =
            ImmutableArray.CreateBuilder<TemplateTypeModelDependency>();

        var visitedTypes =
            new HashSet<ISymbol>(SymbolEqualityComparer.Default);

        AddTypeAndContainingTypeDependencies(
            destinationType,
            compilation,
            result,
            visitedTypes,
            cancellationToken);

        if (destinationType.TypeKind == TypeKind.Interface)
        {
            foreach (var baseInterface in destinationType.AllInterfaces)
            {
                AddTypeAndContainingTypeDependencies(
                    baseInterface,
                    compilation,
                    result,
                    visitedTypes,
                    cancellationToken);
            }
        }
        else
        {
            for (var baseType = destinationType.BaseType;
                 baseType is not null;
                 baseType = baseType.BaseType)
            {
                AddTypeAndContainingTypeDependencies(
                    baseType,
                    compilation,
                    result,
                    visitedTypes,
                    cancellationToken);
            }
        }

        return result.ToImmutable();
    }

    private static void AddTypeAndContainingTypeDependencies(
        INamedTypeSymbol type,
        CSharpCompilation compilation,
        ImmutableArray<TemplateTypeModelDependency>.Builder result,
        HashSet<ISymbol> visitedTypes,
        CancellationToken cancellationToken)
    {
        var containingTypes = new Stack<INamedTypeSymbol>();

        for (var current = type.OriginalDefinition;
             current is not null;
             current = current.ContainingType)
        {
            containingTypes.Push(current);
        }

        while (containingTypes.Count > 0)
        {
            AddTypeDependencies(
                containingTypes.Pop(),
                compilation,
                result,
                visitedTypes,
                cancellationToken);
        }
    }

    private static void AddTypeDependencies(
        INamedTypeSymbol type,
        CSharpCompilation compilation,
        ImmutableArray<TemplateTypeModelDependency>.Builder result,
        HashSet<ISymbol> visitedTypes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!visitedTypes.Add(type))
        {
            return;
        }

        var metadataName = SymbolNameHelper.GetFullMetadataName(type);
        var syntaxReferences = type.DeclaringSyntaxReferences
            .OrderBy(
                static syntaxReference =>
                    syntaxReference.SyntaxTree.FilePath,
                StringComparer.Ordinal)
            .ThenBy(
                static syntaxReference =>
                    syntaxReference.Span.Start)
            .ToArray();

        if (syntaxReferences.Length > 0)
        {
            // A span is not a stable part identity: editing an earlier type in
            // the same file shifts every following declaration.
            var partIndicesByPath = new Dictionary<string, int>(
                StringComparer.Ordinal);

            foreach (var syntaxReference in syntaxReferences)
            {
                var path = syntaxReference.SyntaxTree.FilePath;

                partIndicesByPath.TryGetValue(path, out var partIndex);
                partIndicesByPath[path] = partIndex + 1;

                result.Add(
                    new TemplateTypeModelDependency(
                        metadataName +
                        "|source|" +
                        path +
                        "|" +
                        partIndex.ToString(
                            CultureInfo.InvariantCulture),
                        BuildSourceDependencyVersion(
                            syntaxReference,
                            compilation,
                            cancellationToken)));
            }

            return;
        }

        var metadataReference = compilation.GetMetadataReference(
            type.ContainingAssembly);

        if (metadataReference is not null)
        {
            result.Add(
                new TemplateTypeModelDependency(
                    metadataName +
                    "|metadata|" +
                    type.ContainingAssembly.Identity,
                    metadataReference));
        }
    }

    private static TemplateTypeSourceDependencyVersion
        BuildSourceDependencyVersion(
            SyntaxReference syntaxReference,
            CSharpCompilation compilation,
            CancellationToken cancellationToken)
    {
        var declaration = syntaxReference.GetSyntax(cancellationToken);
        var parseOptions =
            (CSharpParseOptions)syntaxReference.SyntaxTree.Options;

        // Declaration text isolates sibling types in one syntax tree. The
        // semantic context also catches unchanged syntax whose meaning changes
        // through aliases or constants declared elsewhere.
        return new TemplateTypeSourceDependencyVersion(
            declaration.ToFullString(),
            BuildSemanticContext(
                declaration,
                compilation,
                cancellationToken),
            BuildParseOptionsVersion(parseOptions));
    }

    private static string BuildSemanticContext(
        SyntaxNode declaration,
        CSharpCompilation compilation,
        CancellationToken cancellationToken)
    {
        var semanticModel = compilation.GetSemanticModel(
            declaration.SyntaxTree);

        var result = new StringBuilder();

        foreach (var typeSyntax in declaration
                     .DescendantNodesAndSelf()
                     .OfType<TypeSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var type = semanticModel.GetTypeInfo(
                typeSyntax,
                cancellationToken).Type;

            result
                .Append("type|")
                .Append(typeSyntax.SpanStart - declaration.SpanStart)
                .Append('|');

            if (type is null)
            {
                result.Append("<unresolved>");
            }
            else
            {
                result
                    .Append(
                        type.ToDisplayString(
                            SymbolDisplayFormats
                                .FullyQualifiedNullable))
                    .Append('|')
                    .Append((int)type.NullableAnnotation);
            }

            result.AppendLine();
        }

        foreach (var attribute in declaration
                     .DescendantNodesAndSelf()
                     .OfType<AttributeSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var constructor = semanticModel.GetSymbolInfo(
                attribute,
                cancellationToken).Symbol;

            result
                .Append("attribute|")
                .Append(attribute.SpanStart - declaration.SpanStart)
                .Append('|')
                .Append(
                    constructor?.ToDisplayString(
                        SymbolDisplayFormat.FullyQualifiedFormat) ??
                    "<unresolved>")
                .AppendLine();
        }

        foreach (var expression in GetConstantExpressions(declaration))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var constant = semanticModel.GetConstantValue(
                expression,
                cancellationToken);

            var type = semanticModel.GetTypeInfo(
                expression,
                cancellationToken).Type;

            result
                .Append("constant|")
                .Append(expression.SpanStart - declaration.SpanStart)
                .Append('|')
                .Append(
                    type?.ToDisplayString(
                        SymbolDisplayFormats.FullyQualifiedNullable) ??
                    "<unresolved>")
                .Append('|')
                .Append(FormatConstant(constant))
                .AppendLine();
        }

        return result.ToString();
    }

    private static IEnumerable<ExpressionSyntax> GetConstantExpressions(
        SyntaxNode declaration)
    {
        foreach (var argument in declaration
                     .DescendantNodesAndSelf()
                     .OfType<AttributeArgumentSyntax>())
        {
            yield return argument.Expression;
        }

        foreach (var parameter in declaration
                     .DescendantNodesAndSelf()
                     .OfType<ParameterSyntax>())
        {
            if (parameter.Default is { } defaultValue)
            {
                yield return defaultValue.Value;
            }
        }
    }

    private static string FormatConstant(Optional<object?> constant)
    {
        if (!constant.HasValue)
        {
            return "<not-constant>";
        }

        if (constant.Value is null)
        {
            return "null";
        }

        return constant.Value.GetType().FullName + ":" +
               (SymbolDisplay.FormatPrimitive(
                    constant.Value,
                    quoteStrings: true,
                    useHexadecimalNumbers: false) ??
                Convert.ToString(
                    constant.Value,
                    CultureInfo.InvariantCulture));
    }

    private static string BuildParseOptionsVersion(
        CSharpParseOptions parseOptions)
    {
        return parseOptions.LanguageVersion + "|" +
               parseOptions.DocumentationMode + "|" +
               parseOptions.Kind + "|" +
               string.Join(
                   ",",
                   parseOptions.PreprocessorSymbolNames.OrderBy(
                       static symbol => symbol,
                       StringComparer.Ordinal));
    }

    private readonly record struct TemplateTypeModelInput(
        TemplateTypeGenerationInput GenerationInput,
        string HintName,
        INamedTypeSymbol DestinationType,
        CompilationContext Context,
        ImmutableArray<TemplateTypeModelDependency> Dependencies,
        string CompilationAssemblyIdentity,
        NullableContextOptions NullableContextOptions,
        MetadataImportOptions MetadataImportOptions);

    private readonly record struct TemplateTypeModelDependency(
        string Identity,
        object Version);

    private readonly record struct TemplateTypeSourceDependencyVersion(
        string Declaration,
        string SemanticContext,
        string ParseOptions);

    private sealed class TemplateTypeModelInputComparer :
        IEqualityComparer<TemplateTypeModelInput>
    {
        public static TemplateTypeModelInputComparer Instance { get; } =
            new();

        private TemplateTypeModelInputComparer()
        {
        }

        public bool Equals(
            TemplateTypeModelInput x,
            TemplateTypeModelInput y)
        {
            if (x.GenerationInput != y.GenerationInput ||
                !StringComparer.Ordinal.Equals(x.HintName, y.HintName) ||
                x.Context.LanguageVersion != y.Context.LanguageVersion ||
                !StringComparer.Ordinal.Equals(
                    x.CompilationAssemblyIdentity,
                    y.CompilationAssemblyIdentity) ||
                x.NullableContextOptions != y.NullableContextOptions ||
                x.MetadataImportOptions != y.MetadataImportOptions ||
                x.Dependencies.Length != y.Dependencies.Length)
            {
                return false;
            }

            for (var i = 0; i < x.Dependencies.Length; i++)
            {
                if (!StringComparer.Ordinal.Equals(
                        x.Dependencies[i].Identity,
                        y.Dependencies[i].Identity) ||
                    !DependencyVersionsEqual(
                        x.Dependencies[i].Version,
                        y.Dependencies[i].Version))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(TemplateTypeModelInput obj)
        {
            var hash = obj.GenerationInput.GetHashCode();

            hash = AddHash(hash, obj.HintName);
            hash = AddHash(hash, obj.Context.LanguageVersion);
            hash = AddHash(hash, obj.CompilationAssemblyIdentity);
            hash = AddHash(hash, obj.NullableContextOptions);
            hash = AddHash(hash, obj.MetadataImportOptions);

            foreach (var dependency in obj.Dependencies)
            {
                hash = AddHash(hash, dependency.Identity);
                hash = AddDependencyVersionHash(
                    hash,
                    dependency.Version);
            }

            return hash;
        }

        private static bool DependencyVersionsEqual(
            object x,
            object y)
        {
            if (x is TemplateTypeSourceDependencyVersion sourceX &&
                y is TemplateTypeSourceDependencyVersion sourceY)
            {
                return sourceX == sourceY;
            }

            return ReferenceEquals(x, y);
        }

        private static int AddDependencyVersionHash(
            int hash,
            object version)
        {
            var versionHash =
                version is TemplateTypeSourceDependencyVersion source
                    ? source.GetHashCode()
                    : RuntimeHelpers.GetHashCode(version);

            return unchecked(hash * 31 + versionHash);
        }

        private static int AddHash<T>(int hash, T value)
        {
            return unchecked(
                hash * 31 +
                EqualityComparer<T>.Default.GetHashCode(value!));
        }
    }
}
