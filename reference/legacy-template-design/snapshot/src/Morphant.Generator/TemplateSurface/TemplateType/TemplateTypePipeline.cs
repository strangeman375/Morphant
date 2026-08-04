using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Morphant.Generator.TemplateSurface.TemplateType;

internal static class TemplateTypePipeline
{
    public static void Register(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<CompilationContext> compilationContext,
        IncrementalValuesProvider<TemplateDestinationTypeInfo> destinationTypes)
    {
        var generationInputs = destinationTypes
            .Where(static destinationType =>
                destinationType.Kind ==
                TemplateDestinationTypeKind.GeneratedTemplate)
            .Select(static (destinationType, _) =>
                destinationType.TemplateTypeDefinition)
            .WhereHasValue()
            .Collect()
            .SelectMany(static (destinationTypes, cancellationToken) =>
                DeduplicateSortAndAssignHintNames(
                    destinationTypes,
                    cancellationToken));

        var models = TemplateTypeModelPipeline.Build(
            generationInputs,
            compilationContext);

        var requests = models
            .Select(static (model, cancellationToken) =>
                BuildRequest(model, cancellationToken))
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildTemplateTypeRequests);

        context.RegisterSourceOutput(requests, static (context, request) =>
        {
            context.AddSource(
                request.HintName,
                SourceText.From(request.Source, Encoding.UTF8));
        });
    }

    private static TemplateTypeRequest BuildRequest(
        TemplateTypeModelResult model,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return new TemplateTypeRequest(
            model.HintName,
            TemplateTypeEmitter.Emit(model.Model));
    }

    private static ImmutableArray<TemplateTypeGenerationInput>
        DeduplicateSortAndAssignHintNames(
            ImmutableArray<TemplateTypeDefinitionInfo> destinationTypes,
            CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<TemplateTypeDefinitionInfo>();

        foreach (var destinationType in destinationTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (seen.Add(destinationType.MetadataName))
            {
                result.Add(destinationType);
            }
        }

        result.Sort(static (left, right) =>
            StringComparer.Ordinal.Compare(
                left.MetadataName,
                right.MetadataName));

        var generationInputs =
            ImmutableArray.CreateBuilder<TemplateTypeGenerationInput>(
                result.Count);

        var hintNamePartAllocator = new HintNamePartAllocator();

        foreach (var definition in result)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var hintNamePart = hintNamePartAllocator.Allocate(
                definition.MetadataName);

            generationInputs.Add(
                new TemplateTypeGenerationInput(
                    definition,
                    hintNamePart));
        }

        return generationInputs.ToImmutable();
    }
}

internal readonly record struct TemplateTypeGenerationInput(
    TemplateTypeDefinitionInfo Definition,
    string HintNamePart);
