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
    NullDestinationHandlingValue? NullDestinationHandling)
{
    public static MappingSettings Default =>
        new(
            MappingModeValue.Default,
            NullSourceHandlingValue.Default,
            NullDestinationHandlingValue.Default);
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
