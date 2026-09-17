#nullable enable
using Morphant;
using System.Runtime.CompilerServices;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CallerInformation.Calls;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CallerInformation
{
    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
#line 120 "ExtensionCalls.cs"
            builder.Map<int, string>().Convert(input => input.Describe());
#line default
    }
    public static class Scenario
    {
        public static void Verify()
        {
            ITypeMapper<int, string> mapper = new Mapper();
            var expected = OriginalCall(3);
            if (mapper.Create(3) != expected || mapper.Update(3, "old") != expected)
                throw new System.InvalidOperationException("Caller information and optional values must describe the original source invocation.");
        }

        private static string OriginalCall(int input) =>
#line 120 "ExtensionCalls.cs"
            input.Describe(member: "Configure");
#line default
    }
}
