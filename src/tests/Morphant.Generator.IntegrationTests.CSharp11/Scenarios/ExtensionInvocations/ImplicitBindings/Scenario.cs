global using static global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.Existing.Operations;
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Morphant;
using Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.Alternate;
namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings
{
    using static global::Morphant.Generator.IntegrationTests.CSharp11.Scenarios.ExtensionInvocations.ImplicitBindings.Existing.Operations;
    public class AwaitableBase { }
    public sealed class Awaitable : AwaitableBase { }
    public class PairBase { }
    public sealed class Pair : PairBase { }
    public class BagBase : IEnumerable
    {
        public List<int> Items { get; } = new();
        public IEnumerator GetEnumerator() => Items.GetEnumerator();
    }
    public sealed class Bag : BagBase { }
    public sealed class Source
    {
        public int Number { get; set; } = 4;
        public Awaitable Awaitable { get; } = new();
        public Pair Pair { get; } = new();
        public Pair[] Pairs => new[] { Pair };
    }
    [MorphantMapper]
    public partial class AwaitMapper : TypeMapper<AwaitMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Task<int>>().Convert(async source => await source!.Awaitable + source!.Number.Extra());
    }
    [MorphantMapper]
    public partial class DeconstructionMapper : TypeMapper<DeconstructionMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, int>().Convert(source =>
            {
                int first, second;
                (first, second) = source!.Pair;
                return first + second + source!.Number.Extra();
            });
    }
    [MorphantMapper]
    public partial class PatternMapper : TypeMapper<PatternMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, int>().Convert(source =>
                (source!.Pair is (var first, var second) ? first + second : 0) + source!.Number.Extra());
    }
    [MorphantMapper]
    public partial class InitializerMapper : TypeMapper<InitializerMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, int>().Convert(source =>
            {
                var bag = new Bag { source!.Number };
                return bag.Items[0] + source!.Number.Extra();
            });
    }
    [MorphantMapper]
    public partial class LoopDeconstructionMapper : TypeMapper<LoopDeconstructionMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, int>().Convert(source =>
            {
                var total = 0;
                foreach ((int first, int second) in source!.Pairs)
                {
                    total += first + second;
                }
                return total + source!.Number.Extra();
            });
    }
    public static class Scenario
    {
        public static async Task VerifyAwait()
        {
            ITypeMapper<Source, Task<int>> mapper = new AwaitMapper();
            var source = new Source();
            if (await mapper.Create(source) != 17 || await mapper.Update(source, Task.FromResult(0)) != 17)
                throw new InvalidOperationException("An import must preserve the source-selected awaiter.");
        }
        public static void VerifyDeconstruction() => Verify(new DeconstructionMapper(), 17);
        public static void VerifyLoopDeconstruction() => Verify(new LoopDeconstructionMapper(), 17);
        public static void VerifyPattern() => Verify(new PatternMapper(), 17);
        public static void VerifyInitializer() => Verify(new InitializerMapper(), 19);

        private static void Verify(ITypeMapper<Source, int> mapper, int expected)
        {
            var source = new Source();
            if (mapper.Create(source) != expected || mapper.Update(source, 0) != expected)
                throw new InvalidOperationException("An import must preserve implicit deconstruction and Add calls.");
        }
    }
}
