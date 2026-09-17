using System.Runtime.CompilerServices;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionEffects.ForeignCalls
{
    public static class CollectionOperations
    {
        public static void Add<T>(this Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionEffects.ForeignBag bag, T value,
            [CallerMemberName] string member = "")
        {
            Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExtensionInvocations.CollectionEffects.Trace.Events.Add("foreign:add:" + value + ":" + member);
            bag.Value = value + ":" + member;
        }
    }
}
