namespace Morphant.Generator.Settings;

[Flags]
internal enum MappingModeValue
{
    Default = 0,

    MapNew = 1 << 0,

    MapExisting = 1 << 1,

    MapNewAndExisting = MapNew | MapExisting
}

internal readonly record struct MappingSettings(
    MappingModeValue? MappingMode)
{
    public static MappingSettings Default =>
        new(MappingModeValue.Default);

    public static MappingSettings Invalid =>
        new(null);
}

internal readonly record struct EffectiveMappingSettings(
    MappingModeValue? MappingMode)
{
    public bool IsMappingModeValid =>
        MappingMode.HasValue;

    public bool SupportsMapNew =>
        MappingMode is { } mappingMode &&
        (mappingMode & MappingModeValue.MapNew) != 0;

    public bool SupportsMapExisting =>
        MappingMode is { } mappingMode &&
        (mappingMode & MappingModeValue.MapExisting) != 0;

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
                MappingModeValue.MapNewAndExisting));
    }
}
