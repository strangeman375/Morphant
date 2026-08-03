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

internal enum TemplateModeValue
{
    Default = 0,

    Dsl,

    Raw
}

internal readonly record struct MappingSettings(
    MappingModeValue? MappingMode,
    NullSourceHandlingValue? NullSourceHandling,
    NullDestinationHandlingValue? NullDestinationHandling,
    TemplateModeValue? TemplateMode)
{
    public static MappingSettings Default =>
        new(
            MappingModeValue.Default,
            NullSourceHandlingValue.Default,
            NullDestinationHandlingValue.Default,
            TemplateModeValue.Default);
}

internal readonly record struct EffectiveMappingSettings(
    MappingModeValue? MappingMode,
    NullSourceHandlingValue? NullSourceHandling,
    NullDestinationHandlingValue? NullDestinationHandling)
{
    public bool IsMappingModeValid =>
        MappingMode.HasValue;

    public bool IsNullSourceHandlingValid =>
        NullSourceHandling.HasValue;

    public bool IsNullDestinationHandlingValid =>
        NullDestinationHandling.HasValue;

    public bool SupportsMapNew =>
        MappingMode is { } mappingMode &&
        (mappingMode & MappingModeValue.Create) != 0;

    public bool SupportsMapExisting =>
        MappingMode is { } mappingMode &&
        (mappingMode & MappingModeValue.Update) != 0;

    public bool HasExecutableOperation =>
        IsMappingModeValid &&
        IsNullSourceHandlingValid &&
        (SupportsMapNew ||
         SupportsMapExisting &&
         IsNullDestinationHandlingValid);

    public static EffectiveMappingSettings Resolve(
        MappingSettings assemblySettings,
        MappingSettings rootSettings,
        MappingSettings mappingSettings)
    {
        return new EffectiveMappingSettings(
            SettingValueResolver.Resolve(
                assemblySettings.MappingMode,
                rootSettings.MappingMode,
                mappingSettings.MappingMode,
                MappingModeValue.CreateAndUpdate),
            SettingValueResolver.Resolve(
                assemblySettings.NullSourceHandling,
                rootSettings.NullSourceHandling,
                mappingSettings.NullSourceHandling,
                NullSourceHandlingValue.ReturnNull),
            SettingValueResolver.Resolve(
                assemblySettings.NullDestinationHandling,
                rootSettings.NullDestinationHandling,
                mappingSettings.NullDestinationHandling,
                NullDestinationHandlingValue.Create));
    }
}

internal static class EffectiveTemplateMode
{
    public static TemplateModeValue? Resolve(
        MappingSettings assemblySettings,
        MappingSettings rootSettings,
        MappingSettings mappingSettings)
    {
        return SettingValueResolver.Resolve(
            assemblySettings.TemplateMode,
            rootSettings.TemplateMode,
            mappingSettings.TemplateMode,
            TemplateModeValue.Dsl);
    }
}
