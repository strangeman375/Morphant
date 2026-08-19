namespace Morphant.Generator.Settings;

[Flags]
internal enum MappingModeValue
{
    Default = 0,

    Create = 1 << 0,

    Update = 1 << 1,

    CreateAndUpdate = Create | Update
}

internal enum NullSourceHandlingValue
{
    Default = 0,

    ReturnNull,

    ReturnDestination,

    Throw
}

internal enum NullDestinationHandlingValue
{
    Default = 0,

    Create,

    Throw
}

internal enum ConstructorSelectionValue
{
    Default = 0,
    Explicit,
    Parameterless,
    Single,
    Unambiguous,
    Greediest,
    Largest
}

internal enum MemberSelectionValue
{
    Default = 0,
    Auto,
    Explicit
}

internal enum FlatteningValue
{
    Default = 0,
    Auto,
    None
}

internal enum UnmappedMemberValidationValue
{
    Default = 0,
    None,
    Source,
    Destination,
    Strict
}

internal readonly record struct MappingSettings(
    MappingModeValue? MappingMode,
    NullSourceHandlingValue? NullSourceHandling,
    NullDestinationHandlingValue? NullDestinationHandling,
    ConstructorSelectionValue? ConstructorSelection,
    MemberSelectionValue? MemberSelection,
    FlatteningValue? Flattening,
    UnmappedMemberValidationValue? UnmappedMemberValidation,
    InvalidMsBuildSettingValues InvalidMsBuildValues = default)
{
    public static MappingSettings Default =>
        new(
            MappingModeValue.Default,
            NullSourceHandlingValue.Default,
            NullDestinationHandlingValue.Default,
            ConstructorSelectionValue.Default,
            MemberSelectionValue.Default,
            FlatteningValue.Default,
            UnmappedMemberValidationValue.Default);
}

internal readonly record struct InvalidMsBuildSettingValues(
    string? MappingMode,
    string? NullSourceHandling,
    string? NullDestinationHandling,
    string? ConstructorSelection,
    string? MemberSelection,
    string? Flattening,
    string? UnmappedMemberValidation);

internal readonly record struct EffectiveMappingSettings(
    MappingModeValue? MappingMode,
    NullSourceHandlingValue? NullSourceHandling,
    NullDestinationHandlingValue? NullDestinationHandling,
    ConstructorSelectionValue? ConstructorSelection,
    MemberSelectionValue? MemberSelection,
    FlatteningValue? Flattening,
    UnmappedMemberValidationValue? UnmappedMemberValidation)
{
    public bool IsMappingModeValid =>
        MappingMode.HasValue;

    public bool IsNullSourceHandlingValid =>
        NullSourceHandling.HasValue;

    public bool IsNullDestinationHandlingValid =>
        NullDestinationHandling.HasValue;

    public bool IsConstructorSelectionValid =>
        ConstructorSelection.HasValue;

    public bool IsMemberSelectionValid =>
        MemberSelection.HasValue;

    public bool IsFlatteningValid =>
        Flattening.HasValue;

    public bool IsUnmappedMemberValidationValid =>
        UnmappedMemberValidation.HasValue;

    public bool SupportsCreate =>
        MappingMode is { } mappingMode &&
        (mappingMode & MappingModeValue.Create) != 0;

    public bool SupportsUpdate =>
        MappingMode is { } mappingMode &&
        (mappingMode & MappingModeValue.Update) != 0;

    public bool HasExecutableOperation =>
        IsMappingModeValid &&
        IsNullSourceHandlingValid &&
        (SupportsCreate ||
         SupportsUpdate &&
         IsNullDestinationHandlingValid);

    public static EffectiveMappingSettings Resolve(
        MappingSettings assemblySettings,
        IEnumerable<MappingSettings> mappingSettings,
        IEnumerable<MappingSettings> rootSettings)
    {
        var mappingLevels = mappingSettings.ToArray();
        var rootLevels = rootSettings.ToArray();

        IEnumerable<TValue?> Values<TValue>(
            Func<MappingSettings, TValue?> selector)
            where TValue : struct, Enum =>
            mappingLevels.Select(selector)
                .Concat(rootLevels.Select(selector));

        return new EffectiveMappingSettings(
            SettingValueResolver.Resolve(
                assemblySettings.MappingMode,
                Values(static settings => settings.MappingMode),
                MappingModeValue.CreateAndUpdate),
            SettingValueResolver.Resolve(
                assemblySettings.NullSourceHandling,
                Values(static settings => settings.NullSourceHandling),
                NullSourceHandlingValue.ReturnNull),
            SettingValueResolver.Resolve(
                assemblySettings.NullDestinationHandling,
                Values(static settings =>
                    settings.NullDestinationHandling),
                NullDestinationHandlingValue.Create),
            SettingValueResolver.Resolve(
                assemblySettings.ConstructorSelection,
                Values(static settings => settings.ConstructorSelection),
                ConstructorSelectionValue.Unambiguous),
            SettingValueResolver.Resolve(
                assemblySettings.MemberSelection,
                Values(static settings => settings.MemberSelection),
                MemberSelectionValue.Auto),
            SettingValueResolver.Resolve(
                assemblySettings.Flattening,
                Values(static settings => settings.Flattening),
                FlatteningValue.Auto),
            SettingValueResolver.Resolve(
                assemblySettings.UnmappedMemberValidation,
                Values(static settings =>
                    settings.UnmappedMemberValidation),
                UnmappedMemberValidationValue.None));
    }
}
