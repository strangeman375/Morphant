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

        var requests = generationInputs
            .Combine(compilationContext)
            .Select(static (x, cancellationToken) =>
            {
                var (generationInput, context) = x;

                return TryBuild(
                    generationInput,
                    context,
                    cancellationToken);
            })
            .WhereHasValue()
            .WithTrackingName("MorphantGeneratorStageNames.BuildTemplateTypeRequests");

        context.RegisterSourceOutput(requests, static (context, request) =>
        {
            context.AddSource(
                request.HintName,
                SourceText.From(request.Source, Encoding.UTF8));
        });
    }

    private static TemplateTypeRequest? TryBuild(
        TemplateTypeGenerationInput generationInput,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var templateTypeDefinition = generationInput.Definition;

        var destinationType = context.Compilation.GetTypeByMetadataName(
            templateTypeDefinition.MetadataName);

        if (destinationType is null)
        {
            return null;
        }

        var model = TemplateTypeModelBuilder.Build(
            destinationType,
            templateTypeDefinition,
            context.Compilation,
            cancellationToken);

        var hintName =
            "Morphant.TemplateType." +
            generationInput.HintNamePart +
            ".g.cs";

        return new TemplateTypeRequest(
            hintName,
            TemplateTypeEmitter.Emit(model));
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

    private readonly record struct TemplateTypeGenerationInput(
        TemplateTypeDefinitionInfo Definition,
        string HintNamePart);
}
