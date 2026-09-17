#nullable enable
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.Alternate
{
    public static class Operations
    {
        public static int Extra(this int value) => value + 10;
        public static TaskAwaiter<int> GetAwaiter(this global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.Awaitable value) => Task.FromResult(99).GetAwaiter();
        public static void Deconstruct(this global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.Pair value, out int first, out int second) { first = 9; second = 9; }
        public static void Add(this global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.Bag value, int item) => value.Items.Add(99);
    }
}
