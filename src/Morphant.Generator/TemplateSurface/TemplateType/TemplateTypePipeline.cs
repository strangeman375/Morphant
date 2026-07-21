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
        var destinationDefinitions = destinationTypes
            .Where(static destinationType =>
                destinationType.Kind ==
                TemplateDestinationTypeKind.GeneratedTemplate)
            .Select(static (destinationType, _) =>
                destinationType.TemplateTypeDefinition)
            .WhereHasValue()
            .Collect()
            .SelectMany(static (destinationTypes, cancellationToken) =>
                DeduplicateAndSortDefinitions(
                    destinationTypes,
                    cancellationToken));

        var requests = destinationDefinitions
            .Combine(compilationContext)
            .Select(static (x, cancellationToken) =>
            {
                var (templateTypeDefinition, context) = x;

                return TryBuild(
                    templateTypeDefinition,
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
        TemplateTypeDefinitionInfo templateTypeDefinition,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
            HintNameHelper.ToHintNamePart(
                templateTypeDefinition.MetadataName) +
            ".g.cs";

        return new TemplateTypeRequest(
            hintName,
            TemplateTypeEmitter.Emit(model));
    }

    private static ImmutableArray<TemplateTypeDefinitionInfo>
        DeduplicateAndSortDefinitions(
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

        return result.ToImmutableArray();
    }
}
