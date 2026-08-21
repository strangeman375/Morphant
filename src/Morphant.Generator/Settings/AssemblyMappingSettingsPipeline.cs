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

    private const string UnknownDerivedTypeHandlingPropertyName =
        "build_property.MorphantUnknownDerivedTypeHandling";

    private const string ConstructorSelectionPropertyName =
        "build_property.MorphantConstructorSelection";

    private const string MemberSelectionPropertyName =
        "build_property.MorphantMemberSelection";

    private const string FlatteningPropertyName =
        "build_property.MorphantFlattening";

    private const string UnmappedMemberValidationPropertyName =
        "build_property.MorphantUnmappedMemberValidation";

    public static IncrementalValueProvider<MappingSettings> Build(
        IncrementalGeneratorInitializationContext context)
    {
        return context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
            {
                var globalOptions = options.GlobalOptions;
                var mappingMode = ParseNamedValue<MappingModeValue>(
                    GetValue(globalOptions, MappingModePropertyName));
                var nullSource = ParseNamedValue<NullSourceHandlingValue>(
                    GetValue(globalOptions, NullSourceHandlingPropertyName));
                var nullDestination =
                    ParseNamedValue<NullDestinationHandlingValue>(
                        GetValue(
                            globalOptions,
                            NullDestinationHandlingPropertyName));
                var unknownDerived =
                    ParseNamedValue<UnknownDerivedTypeHandlingValue>(
                        GetValue(
                            globalOptions,
                            UnknownDerivedTypeHandlingPropertyName));
                var constructor =
                    ParseNamedValue<ConstructorSelectionValue>(
                        GetValue(
                            globalOptions,
                            ConstructorSelectionPropertyName));
                var member = ParseNamedValue<MemberSelectionValue>(
                    GetValue(globalOptions, MemberSelectionPropertyName));
                var flattening = ParseNamedValue<FlatteningValue>(
                    GetValue(globalOptions, FlatteningPropertyName));
                var validation =
                    ParseNamedValue<UnmappedMemberValidationValue>(
                        GetValue(
                            globalOptions,
                            UnmappedMemberValidationPropertyName));

                return new MappingSettings(
                    mappingMode.Value,
                    nullSource.Value,
                    nullDestination.Value,
                    unknownDerived.Value,
                    constructor.Value,
                    member.Value,
                    flattening.Value,
                    validation.Value,
                    new InvalidMsBuildSettingValues(
                        mappingMode.InvalidValue,
                        nullSource.InvalidValue,
                        nullDestination.InvalidValue,
                        unknownDerived.InvalidValue,
                        constructor.InvalidValue,
                        member.InvalidValue,
                        flattening.InvalidValue,
                        validation.InvalidValue));
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

    private static ParsedMappingSetting<TValue> ParseNamedValue<TValue>(
        string? value)
        where TValue : struct, Enum
    {
        if (value is null)
        {
            return new ParsedMappingSetting<TValue>(default(TValue), null);
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length == 0)
        {
            return new ParsedMappingSetting<TValue>(default(TValue), null);
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
            return new ParsedMappingSetting<TValue>(null, normalizedValue);
        }

        return new ParsedMappingSetting<TValue>(parsedValue, null);
    }

    private readonly record struct ParsedMappingSetting<TValue>(
        TValue? Value,
        string? InvalidValue)
        where TValue : struct, Enum;
}
