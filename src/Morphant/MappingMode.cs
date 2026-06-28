namespace Morphant;

[Flags]
public enum MappingMode
{
    Default = 0,

    MapNew = 1 << 0,

    MapExisting = 1 << 1,

    MapNewAndExisting = MapNew | MapExisting
}
