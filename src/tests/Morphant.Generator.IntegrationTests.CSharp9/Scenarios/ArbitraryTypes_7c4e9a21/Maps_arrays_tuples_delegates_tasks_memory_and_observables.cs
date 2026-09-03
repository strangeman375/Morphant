// Compiled integration scenario: TypeMapperArbitraryTypeTests::Maps_arrays_tuples_delegates_tasks_memory_and_observables

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ArbitraryTypes_7c4e9a21
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
    public partial class ArbitraryTypeMapper : TypeMapper<ArbitraryTypeMapper>
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
            var mapper = new ArbitraryTypeMapper();
            var list = ((ITypeMapper<int[], List<int>>)mapper)
                .Create(new[] { 1, 2 });
            var tuple = ((ITypeMapper<
                    (int Id, string Name),
                    string>)mapper).Create(
                (3, "three"));
            var fromDelegate = ((ITypeMapper<Func<int>, int>)mapper)
                .Create(() => 4);
            var fromExpression = ((ITypeMapper<
                    Expression<Func<int>>,
                    int>)mapper).Create(
                () => 5);
            var fromTask = ((ITypeMapper<Task<int>, int>)mapper)
                .Create(Task.FromResult(6));
            var fromMemory = ((ITypeMapper<Memory<int>, int>)mapper).Create(
                new Memory<int>(new[] { 7 }));
            var fromObservable = ((ITypeMapper<
                    ObservableValue,
                    int>)mapper).Create(
                new ObservableValue(8));
            var array = ((ITypeMapper<int, int[]>)mapper).Create(9);
            var listContract = (ITypeMapper<int, List<int>>)mapper;
            var createdList = listContract.Create(10);
            var updatedList = listContract.Update(11, createdList);
            var deferred = ((ITypeMapper<int, Task<int>>)mapper).Create(12);

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
                    "Arbitrary source and destination types did not preserve " +
                    "their configured runtime mapping policies.");
            }
        }
    }
}
