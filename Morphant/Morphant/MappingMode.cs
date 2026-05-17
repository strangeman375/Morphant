namespace Morphant;

[Flags]
public enum MappingMode
{
    Default = 0,

    MapNew = 1 << 0,

    MapExisting = 1 << 1,

    Map = MapNew | MapExisting,

    Project = 1 << 2,

    MapNewAndProject = MapNew | Project,

    MapExistingAndProject = MapExisting | Project,

    MapAndProject = Map | Project
}
