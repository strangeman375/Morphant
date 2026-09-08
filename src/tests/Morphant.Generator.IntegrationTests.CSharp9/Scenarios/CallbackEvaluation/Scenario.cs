#nullable enable
using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CallbackEvaluation
{
    public sealed class Source { public int Value { get; set; } }
    public sealed class DelegateTag { }
    public sealed class FinallyTag { }
    public sealed class Destination<T>
    {
        public Destination(int value) => Value = value;
        public int Value { get; }
    }
    public sealed class DeferredDestination
    {
        public int Value { get; set; }
        public Func<int> Later { get; set; } = () => 0;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public int Offset { get; set; }
        public int DelegateReads { get; private set; }
        public int FinallyCalls { get; private set; }
        public int DeferredReads { get; private set; }
        private Func<Source?, Destination<DelegateTag>> Callback
        {
            get
            {
                DelegateReads++;
                var offset = Offset;
                return source => new(source!.Value + offset);
            }
        }
        private int ReadLater(Source source) { DeferredReads++; return source.Value; }
        private static int Fail() => throw new InvalidOperationException("An unused structured local was evaluated.");

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination<DelegateTag>>().Convert(Callback);
            builder.Map<Source, Destination<FinallyTag>>().Convert(source =>
            {
                try
                {
                    if (source is null) return new(-1);
                    return new(source.Value);
                }
                finally
                {
                    FinallyCalls++;
                }
            });
            builder.Map<Source, DeferredDestination>().Members(source =>
            {
                var unused = Fail();
                return new()
                {
                    Value = source.Value,
                    Later = Value<Func<int>>(() => ReadLater(source))
                };
            });
        }
    }

    public static class Scenario
    {
        public static void VerifyDelegateProperty()
        {
            var concrete = new TestMapper { Offset = 5 };
            var mapper = (ITypeMapper<Source, Destination<DelegateTag>>)concrete;
            var source = new Source { Value = 2 };
            var first = mapper.Create(source);
            if (first.Value != 7 || concrete.DelegateReads != 1)
                throw new InvalidOperationException("Create must read and invoke the delegate property once.");
            concrete.Offset = 10;
            var second = mapper.Create(source);
            if (second.Value != 12 || concrete.DelegateReads != 2)
                throw new InvalidOperationException("A later Create must obtain a new delegate with current mapper state.");
            concrete.Offset = 20;
            var updated = mapper.Update(source, first);
            if (updated.Value != 22 || concrete.DelegateReads != 3)
                throw new InvalidOperationException("Update must also reevaluate the delegate property exactly once.");
        }

        public static void VerifyFinallyOnNullReturn(bool update)
        {
            var concrete = new TestMapper();
            var mapper = (ITypeMapper<Source, Destination<FinallyTag>>)concrete;
            var result = update ? mapper.Update(null, new Destination<FinallyTag>(19)) : mapper.Create(null);
            if (result.Value != -1 || concrete.FinallyCalls != 1)
                throw new InvalidOperationException("A null-source return inside try must execute finally exactly once.");
        }

        public static void VerifyDeferredSourceCapture()
        {
            var concrete = new TestMapper();
            var mapper = (ITypeMapper<Source, DeferredDestination>)concrete;
            var source = new Source { Value = 20 };
            var result = mapper.Create(source);
            if (result.Value != 20 || concrete.DeferredReads != 0)
                throw new InvalidOperationException("Creating a delegate must not evaluate its body or unrelated structured locals.");
            source.Value = 30;
            if (result.Later() != 30 || concrete.DeferredReads != 1)
                throw new InvalidOperationException("The deferred expression must read the captured source when invoked.");
        }
    }
}
