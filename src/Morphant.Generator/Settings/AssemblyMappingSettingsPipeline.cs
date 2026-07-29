using Microsoft.CodeAnalysis;

namespace Morphant.Generator.Settings;

internal static class AssemblyMappingSettingsPipeline
{
    private const string MappingModePropertyName =
        "build_property.MorphantMappingMode";

    public static IncrementalValueProvider<MappingSettings> Build(
        IncrementalGeneratorInitializationContext context)
    {
        return context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
                ParseMappingMode(
                    options.GlobalOptions.TryGetValue(
                        MappingModePropertyName,
                        out var value)
                        ? value
                        : null))
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildAssemblyMappingSettings);
    }

    private static MappingSettings ParseMappingMode(string? value)
    {
        if (value is null)
        {
            return MappingSettings.Default;
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length == 0)
        {
            return MappingSettings.Default;
        }

        MappingModeValue mappingMode;

        if (normalizedValue.Equals(
                nameof(MappingModeValue.Default),
                StringComparison.OrdinalIgnoreCase))
        {
            mappingMode = MappingModeValue.Default;
        }
        else if (normalizedValue.Equals(
                     nameof(MappingModeValue.MapNew),
                     StringComparison.OrdinalIgnoreCase))
        {
            mappingMode = MappingModeValue.MapNew;
        }
        else if (normalizedValue.Equals(
                     nameof(MappingModeValue.MapExisting),
                     StringComparison.OrdinalIgnoreCase))
        {
            mappingMode = MappingModeValue.MapExisting;
        }
        else if (normalizedValue.Equals(
                     nameof(MappingModeValue.MapNewAndExisting),
                     StringComparison.OrdinalIgnoreCase))
        {
            mappingMode = MappingModeValue.MapNewAndExisting;
        }
        else
        {
            return MappingSettings.Invalid;
        }

        return new MappingSettings(mappingMode);
    }
}
