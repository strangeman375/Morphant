#nullable enable
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.SourceScope.Mappers
{
    using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.SourceScope.Imported;
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<string, int>().Convert(source =>
                source!.Describe() + source!.Length.Independent());
    }
    public static class Scenario
    {
        public static void Verify()
        {
            ITypeMapper<string, int> mapper = new Mapper();
            if (mapper.Create("abc") != 17 || mapper.Update("abc", 0) != 17)
                throw new System.InvalidOperationException("An import must not replace the source-selected overload with the closer extension.");
        }
    }
}
