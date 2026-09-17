#nullable enable
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ConditionalFallback.Mappers
{
    using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ConditionalFallback.Calls;
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<string, int>().Convert(source => source?.Echo().LengthPlus() ?? -1);
    }
    public static class Scenario
    {
        public static void Verify()
        {
            ITypeMapper<string, int> mapper = new Mapper();
            if (mapper.Create("abc") != 9 || mapper.Update("abc", 0) != 9 || mapper.Create(null) != -1 || mapper.Update(null, 0) != -1)
                throw new System.InvalidOperationException("A conditional fallback must retain the original method and null propagation.");
        }
    }
}
