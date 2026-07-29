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
    MappingModeValue MappingMode)
{
    public static MappingSettings Default =>
        new(MappingModeValue.Default);
}

internal readonly record struct EffectiveMappingSettings(
    MappingModeValue MappingMode)
{
    public bool SupportsMapNew =>
        (MappingMode & MappingModeValue.MapNew) != 0;

    public bool SupportsMapExisting =>
        (MappingMode & MappingModeValue.MapExisting) != 0;

    public static EffectiveMappingSettings Resolve(
        MappingSettings rootSettings,
        MappingSettings mappingSettings)
    {
        var mappingMode =
            mappingSettings.MappingMode != MappingModeValue.Default
                ? mappingSettings.MappingMode
                : rootSettings.MappingMode;

        if (mappingMode == MappingModeValue.Default)
        {
            mappingMode = MappingModeValue.MapNewAndExisting;
        }

        return new EffectiveMappingSettings(mappingMode);
    }
}
