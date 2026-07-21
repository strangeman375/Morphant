using System.Collections.Immutable;
using System.Globalization;
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

        var usedHintNameParts =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var definition in result)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var readableHintNamePart =
                HintNameHelper.ToReadableHintNamePart(
                    definition.MetadataName);

            var hintNamePart = readableHintNamePart;

            if (!usedHintNameParts.Add(hintNamePart))
            {
                hintNamePart = HintNameHelper.AppendStableHash(
                    readableHintNamePart,
                    definition.MetadataName);

                var collisionIndex = 2;

                while (!usedHintNameParts.Add(hintNamePart))
                {
                    hintNamePart =
                        HintNameHelper.AppendStableHash(
                            readableHintNamePart,
                            definition.MetadataName) +
                        "_" +
                        collisionIndex.ToString(
                            CultureInfo.InvariantCulture);

                    collisionIndex++;
                }
            }

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
