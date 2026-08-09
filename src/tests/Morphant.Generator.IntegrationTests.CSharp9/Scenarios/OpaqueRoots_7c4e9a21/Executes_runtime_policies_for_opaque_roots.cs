// Compiled integration scenario: TypeMapperOpaqueRootTests::Executes_runtime_policies_for_opaque_roots

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.OpaqueRoots_7c4e9a21
{
    public sealed class ObservableValue : IObservable<int>
    {
        public ObservableValue(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public IDisposable Subscribe(IObserver<int> observer) =>
            throw new NotSupportedException();
    }

    [MorphantMapper]
    public partial class OpaqueMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<int[], List<int>>()
                .Convert(source =>
                    new List<int>(source ?? Array.Empty<int>()));
            builder.Map<(int Id, string Name), string>()
                .ConstructUsing(source => source.Id + ":" + source.Name);
            builder.Map<Func<int>, int>()
                .ConstructUsing(source => source());
            builder.Map<Expression<Func<int>>, int>()
                .Convert(source => source is null
                    ? -1
                    : source.Compile()());
            builder.Map<Task<int>, int>()
                .Convert(source => source is null
                    ? -1
                    : source.GetAwaiter().GetResult());
            builder.Map<Memory<int>, int>()
                .Convert(source => source.Span[0]);
            builder.Map<ObservableValue, int>()
                .Convert(source => source?.Value ?? -1);
            builder.Map<int, int[]>()
                .ConstructUsing(source => new[] { source });
            builder.Map<int, List<int>>()
                .ResolveUsing((source, previous) =>
                {
                    if (previous.TryGetValue(out var result))
                    {
                        result.Add(source);
                        return result;
                    }

                    return new List<int> { source };
                });
            builder.Map<int, Task<int>>()
                .ConstructUsing(Task.FromResult);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new OpaqueMapper();
            var list = mapper.Create<int[], List<int>>(new[] { 1, 2 });
            var tuple = mapper.Create<(int Id, string Name), string>(
                (3, "three"));
            var fromDelegate = mapper.Create<Func<int>, int>(() => 4);
            var fromExpression = mapper.Create<Expression<Func<int>>, int>(
                () => 5);
            var fromTask = mapper.Create<Task<int>, int>(Task.FromResult(6));
            var fromMemory = mapper.Create<Memory<int>, int>(
                new Memory<int>(new[] { 7 }));
            var fromObservable = mapper.Create<ObservableValue, int>(
                new ObservableValue(8));
            var array = mapper.Create<int, int[]>(9);
            var createdList = mapper.Create<int, List<int>>(10);
            var updatedList = mapper.Update(11, createdList);
            var deferred = mapper.Create<int, Task<int>>(12);

            if (list.Count != 2 || list[0] != 1 || list[1] != 2 ||
                tuple != "3:three" ||
                fromDelegate != 4 ||
                fromExpression != 5 ||
                fromTask != 6 ||
                fromMemory != 7 ||
                fromObservable != 8 ||
                array.Length != 1 || array[0] != 9 ||
                !ReferenceEquals(createdList, updatedList) ||
                updatedList.Count != 2 ||
                updatedList[0] != 10 || updatedList[1] != 11 ||
                deferred.GetAwaiter().GetResult() != 12)
            {
                throw new InvalidOperationException(
                    "Opaque roots did not preserve the configured runtime " +
                    "mapping policies.");
            }
        }
    }
}
