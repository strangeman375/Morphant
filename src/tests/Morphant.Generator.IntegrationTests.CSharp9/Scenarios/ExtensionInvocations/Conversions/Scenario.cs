#nullable enable
using System;
using Morphant;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Conversions.Calls;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.Conversions
{
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<int, string>().Convert(source =>
            {
                int value = source;
                var first = value.Increase(2);
                return first.Read().Choose(number => number + 1).Receive(value) + ":" + value;
            });
    }
    public static class Scenario
    {
        public static void Verify()
        {
            ITypeMapper<int, string> mapper = new Mapper();
            Calls.Number.Conversions = 0;
            if (mapper.Create(3) != "12:5" || Calls.Number.Conversions != 1)
                throw new System.InvalidOperationException("Ref receivers, generic overload inference and user conversions must be preserved.");
            if (mapper.Update(3, "old") != "12:5" || Calls.Number.Conversions != 2)
                throw new System.InvalidOperationException("Update must evaluate the same conversions once.");
        }
    }
}
