using Morphant;

namespace Morphant.Generator.UnitTests.TestAssets.ConfigurationOverridesScenario;

public abstract class MetadataConfigurationBase : TypeMapper
{
    protected override void Configure(MapperBuilder builder)
    {
        builder.MappingMode(MappingMode.Create);
    }
}
