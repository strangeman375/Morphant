#nullable enable
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.Existing
{
    public static class Operations
    {
        public static TaskAwaiter<int> GetAwaiter(this global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.AwaitableBase value) => Task.FromResult(3).GetAwaiter();
        public static void Deconstruct(this global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.PairBase value, out int first, out int second) { first = 1; second = 2; }
        public static void Add(this global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.BagBase value, int item) => value.Items.Add(item + 1);
    }
}
