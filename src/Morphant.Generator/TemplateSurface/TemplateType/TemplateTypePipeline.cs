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
        var requests = destinationTypes
            .Combine(compilationContext)
            .Select(static (x, cancellationToken) =>
            {
                var (destinationTypeInfo, context) = x;

                return TryBuild(
                    destinationTypeInfo,
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
        TemplateDestinationTypeInfo destinationTypeInfo,
        CompilationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var destinationType = context.Compilation.GetTypeByMetadataName(
            destinationTypeInfo.MetadataName);

        if (destinationType is null)
        {
            return null;
        }

        var model = TemplateTypeModelBuilder.Build(
            destinationType,
            destinationTypeInfo,
            context.Compilation,
            cancellationToken);

        var hintName =
            "Morphant.TemplateType." +
            HintNameHelper.ToHintNamePart(destinationTypeInfo.MetadataName) +
            ".g.cs";

        return new TemplateTypeRequest(
            hintName,
            TemplateTypeEmitter.Emit(model));
    }
}
