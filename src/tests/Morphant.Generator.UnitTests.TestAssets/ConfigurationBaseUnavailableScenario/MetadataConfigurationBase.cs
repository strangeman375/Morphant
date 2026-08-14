using Morphant;

namespace Morphant.Generator.UnitTests.TestAssets.ConfigurationBaseUnavailableScenario;

public abstract class MetadataConfigurationBase : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.MappingMode(MappingMode.Create);
    }
}
