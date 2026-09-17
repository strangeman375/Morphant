#nullable enable
using System;
using System.Collections.Generic;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ImportIsolation.Calls;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ImportIsolation.SafeCalls;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.ImportIsolation
{
    using System.Linq;
    public sealed class Source
    {
        public int[] Values { get; set; } = new[] { 1, 2 };
        public string Text { get; set; } = "abc";
        public int Increment(int value) => value + 1;
    }
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<string, int>().Convert(source => source!.Extra() + source!.Length.Twice());
            builder.Map<Source, string>().Convert(source =>
            {
                Func<int, int> increment = source!.Increment;
                return string.Join(",", from value in source!.Values select increment(value));
            });
        }
    }
    public static class Scenario
    {
        public static void Verify()
        {
            var concrete = new Mapper();
            ITypeMapper<string, int> textMapper = concrete;
            ITypeMapper<Source, string> queryMapper = concrete;
            if (textMapper.Create("abc") != 9 || textMapper.Update("abc", 0) != 9)
                throw new System.InvalidOperationException("The conflicting extension must retain static binding while the independent extension stays usable.");
            var source = new Source();
            if (queryMapper.Create(source) != "2,3" || queryMapper.Update(source, "old") != "2,3")
                throw new System.InvalidOperationException("An extension import must not rebind query operators in another mapping.");
        }
    }
}
