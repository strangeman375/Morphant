using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Morphant.Generator.TemplateSurface.TemplateType;

internal static class TemplateTypeModelPipeline
{
    public static IncrementalValuesProvider<TemplateTypeModelResult> Build(
        IncrementalValuesProvider<TemplateTypeGenerationInput> generationInputs,
        IncrementalValueProvider<CompilationContext> compilationContext)
    {
        // CompilationProvider changes after every source edit. Reduce that
        // broad input to the syntax trees and references that can affect this
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

        var hintName =
            "Morphant.TemplateType." +
            generationInput.HintNamePart +
            ".g.cs";

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
            foreach (var syntaxReference in syntaxReferences)
            {
                result.Add(
                    new TemplateTypeModelDependency(
                        metadataName +
                        "|source|" +
                        syntaxReference.SyntaxTree.FilePath +
                        "|" +
                        syntaxReference.Span.Start.ToString(
                            CultureInfo.InvariantCulture),
                        syntaxReference.SyntaxTree));
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
                    !ReferenceEquals(
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
                hash = unchecked(
                    hash * 31 +
                    RuntimeHelpers.GetHashCode(dependency.Version));
            }

            return hash;
        }

        private static int AddHash<T>(int hash, T value)
        {
            return unchecked(
                hash * 31 +
                EqualityComparer<T>.Default.GetHashCode(value!));
        }
    }
}
