#nullable enable
using System;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ConditionalFallbackMembers.Mappers
{
    using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ConditionalFallbackMembers.Calls;
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, int>().Convert(source =>
        {
            var property = source /* receiver */ ? /* question */ .Text.Describe() ?? 0;
            var indexer = source?[0].Describe() ?? 0;
            var method = source?.GetText().Describe() ?? 0;
            source // statement receiver
                ? // statement question
                .Text.Record();
            Action action = () => source /* lambda receiver */ ? /* lambda question */ .Text.Record();
            void Record() => source /* function receiver */ ? /* function question */ .Text.Record();
            action();
            Record();
            return property + indexer + method + 1.Independent();
        });
    }
    public static class Scenario
    {
        public static void Verify()
        {
            ITypeMapper<Source, int> mapper = new Mapper();
            var source = new Source();
            Source.Records = 0;
            if (mapper.Create(source) != 35 || source.Reads != 6 || Source.Records != 3)
                throw new InvalidOperationException("Create must preserve every read and the original extension overloads.");
            if (mapper.Update(source, 0) != 35 || source.Reads != 12 || Source.Records != 6)
                throw new InvalidOperationException("Update must preserve every read and the original extension overloads.");
            if (mapper.Create(null) != 2 || mapper.Update(null, 0) != 2 || Source.Records != 6)
                throw new InvalidOperationException("Null receivers must skip all conditional effects.");
        }
    }
}
