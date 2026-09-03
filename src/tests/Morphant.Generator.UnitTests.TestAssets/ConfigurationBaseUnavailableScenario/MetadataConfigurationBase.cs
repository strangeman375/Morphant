using Morphant;

namespace Morphant.Generator.UnitTests.TestAssets.ConfigurationBaseUnavailableScenario;

public abstract class MetadataConfigurationBase<TMapper> : TypeMapper<TMapper>
    where TMapper : MetadataConfigurationBase<TMapper>
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.MappingMode(MappingMode.Create);
    }
}
