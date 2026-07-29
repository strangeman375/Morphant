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
        MappingSettings rootSettings,
        MappingSettings mappingSettings)
    {
        if (mappingSettings.MappingMode is not { } mappingMode)
        {
            return new EffectiveMappingSettings(null);
        }

        if (mappingMode == MappingModeValue.Default)
        {
            if (rootSettings.MappingMode is not { } rootMappingMode)
            {
                return new EffectiveMappingSettings(null);
            }

            mappingMode = rootMappingMode;
        }

        if (mappingMode == MappingModeValue.Default)
        {
            mappingMode = MappingModeValue.MapNewAndExisting;
        }

        return new EffectiveMappingSettings(mappingMode);
    }
}
