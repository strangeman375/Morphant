// Compiled integration scenario: RegistrationDiagnosticsTests::Executes_runtime_and_manual_policies_for_every_opaque_root_family
#nullable enable
#pragma warning disable CS1591

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.RegistrationOpaque_48bd10ee
{
    public sealed record RuntimeResult(int Value);

    public sealed record ManualResult(int Value);

    public sealed class PoisonCollection : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator() =>
            throw new InvalidOperationException(
                "Morphant enumerated an opaque source.");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class PoisonObservable : IObservable<int>
    {
        public IDisposable Subscribe(IObserver<int> observer) =>
            throw new InvalidOperationException(
                "Morphant subscribed to an opaque source.");
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<(int Id, string Name), RuntimeResult>()
                .ConstructUsing(source => new RuntimeResult(11));
            builder.Map<(int Id, string Name), ManualResult>()
                .Convert(source => new ManualResult(12));
            builder.Map<int[], RuntimeResult>()
                .ConstructUsing(source => new RuntimeResult(21));
            builder.Map<int[], ManualResult>()
                .Convert(source => new ManualResult(22));
            builder.Map<PoisonCollection, RuntimeResult>()
                .ConstructUsing(source => new RuntimeResult(31));
            builder.Map<PoisonCollection, ManualResult>()
                .Convert(source => new ManualResult(32));
            builder.Map<Func<int>, RuntimeResult>()
                .ConstructUsing(source => new RuntimeResult(41));
            builder.Map<Func<int>, ManualResult>()
                .Convert(source => new ManualResult(42));
            builder.Map<Expression<Func<int>>, RuntimeResult>()
                .ConstructUsing(source => new RuntimeResult(51));
            builder.Map<Expression<Func<int>>, ManualResult>()
                .Convert(source => new ManualResult(52));
            builder.Map<Task<int>, RuntimeResult>()
                .ConstructUsing(source => new RuntimeResult(61));
            builder.Map<Task<int>, ManualResult>()
                .Convert(source => new ManualResult(62));
            builder.Map<ValueTask<int>, RuntimeResult>()
                .ConstructUsing(source => new RuntimeResult(71));
            builder.Map<ValueTask<int>, ManualResult>()
                .Convert(source => new ManualResult(72));
            builder.Map<PoisonObservable, RuntimeResult>()
                .ConstructUsing(source => new RuntimeResult(81));
            builder.Map<PoisonObservable, ManualResult>()
                .Convert(source => new ManualResult(82));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();

            Verify(mapper, ((1, "one")), 11, 12);
            Verify(mapper, new[] { 1, 2 }, 21, 22);
            Verify(mapper, new PoisonCollection(), 31, 32);
            Verify(mapper, new Func<int>(() =>
                throw new InvalidOperationException(
                    "Morphant invoked an opaque delegate.")), 41, 42);
            Verify(mapper, (Expression<Func<int>>)(() => 5), 51, 52);
            Verify(mapper, Task.FromResult(6), 61, 62);
            Verify(mapper, new ValueTask<int>(7), 71, 72);
            Verify(mapper, new PoisonObservable(), 81, 82);
        }

        private static void Verify<TSource>(
            TestMapper mapper,
            TSource source,
            int runtimeValue,
            int manualValue)
        {
            var runtime = (ITypeMapper<TSource, RuntimeResult>)mapper;
            var manual = (ITypeMapper<TSource, ManualResult>)mapper;

            if (runtime.Create(source, default(MappingContext)).Value !=
                    runtimeValue ||
                manual.Create(source, default(MappingContext)).Value !=
                    manualValue)
            {
                throw new InvalidOperationException(
                    "An opaque root did not execute its configured policy.");
            }
        }
    }
}
