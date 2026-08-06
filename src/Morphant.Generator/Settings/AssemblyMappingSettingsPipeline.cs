using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Morphant.Generator.Settings;

internal static class AssemblyMappingSettingsPipeline
{
    private const string MappingModePropertyName =
        "build_property.MorphantMappingMode";

    private const string NullSourceHandlingPropertyName =
        "build_property.MorphantNullSourceHandling";

    private const string NullDestinationHandlingPropertyName =
        "build_property.MorphantNullDestinationHandling";

    private const string ConstructorSelectionPropertyName =
        "build_property.MorphantConstructorSelection";

    private const string MemberSelectionPropertyName =
        "build_property.MorphantMemberSelection";

    private const string UnmappedMemberValidationPropertyName =
        "build_property.MorphantUnmappedMemberValidation";

    public static IncrementalValueProvider<MappingSettings> Build(
        IncrementalGeneratorInitializationContext context)
    {
        return context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
            {
                var globalOptions = options.GlobalOptions;

                return new MappingSettings(
                    ParseNamedValue<MappingModeValue>(
                        GetValue(
                            globalOptions,
                            MappingModePropertyName)),
                    ParseNamedValue<NullSourceHandlingValue>(
                        GetValue(
                            globalOptions,
                            NullSourceHandlingPropertyName)),
                    ParseNamedValue<NullDestinationHandlingValue>(
                        GetValue(
                            globalOptions,
                            NullDestinationHandlingPropertyName)),
                    ParseNamedValue<ConstructorSelectionValue>(
                        GetValue(
                            globalOptions,
                            ConstructorSelectionPropertyName)),
                    ParseNamedValue<MemberSelectionValue>(
                        GetValue(
                            globalOptions,
                            MemberSelectionPropertyName)),
                    ParseNamedValue<UnmappedMemberValidationValue>(
                        GetValue(
                            globalOptions,
                            UnmappedMemberValidationPropertyName)));
            })
            .WithTrackingName(
                MorphantGeneratorStageNames.BuildAssemblyMappingSettings);
    }

    private static string? GetValue(
        AnalyzerConfigOptions options,
        string propertyName)
    {
        return options.TryGetValue(
            propertyName,
            out var value)
                ? value
                : null;
    }

    private static TValue? ParseNamedValue<TValue>(
        string? value)
        where TValue : struct, Enum
    {
        if (value is null)
        {
            return default(TValue);
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length == 0)
        {
            return default(TValue);
        }

        if (!Enum.TryParse<TValue>(
                normalizedValue,
                ignoreCase: true,
                out var parsedValue) ||
            !Enum.IsDefined(
                typeof(TValue),
                parsedValue) ||
            Enum.GetName(
                typeof(TValue),
                parsedValue) is not { } name ||
            !normalizedValue.Equals(
                name,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return parsedValue;
    }
}
