namespace Morphant.Generator;

internal static class MorphantGeneratorStageNames
{
    public const string BuildCompilationContext = nameof(BuildCompilationContext);

    public const string FindMorphantMapperDeclarations = nameof(FindMorphantMapperDeclarations);

    public const string FindTypeMapperConfigureCandidates = nameof(FindTypeMapperConfigureCandidates);
    public const string BuildTypeMapperConfigureInfos = nameof(BuildTypeMapperConfigureInfos);

    public const string BuildTemplateDestinationTypeInfos = nameof(BuildTemplateDestinationTypeInfos);
    public const string CollectTemplateDestinationTypeInfos = nameof(CollectTemplateDestinationTypeInfos);

    public const string BuildMapperBuilderMapInfos = nameof(BuildMapperBuilderMapInfos);

    public const string BuildTemplateSurface = nameof(BuildTemplateSurface);
    public const string BuildTypeMappers = nameof(BuildTypeMappers);

    public const string ReportDiagnostics = nameof(ReportDiagnostics);
}
