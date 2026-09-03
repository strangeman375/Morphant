// Compiled integration scenario: first-class tuple name conventions
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TupleConvention_a7b10001
{
    public sealed record ObjectSource(int Id, string Name, int Extra);

    public sealed class ObjectDestination
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public sealed record RecordDestination(int id, string name);

    public sealed record NestedValues(int Id, string Name);

    public sealed record IncludedSource(NestedValues Values);

    public sealed record FlattenedSource(NestedValues Customer);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ObjectSource, (string Name, int Id)>();
            builder.Map<(int Id, string Name), ObjectDestination>();
            builder.Map<(int Id, string Name), RecordDestination>();
            builder.Map<IncludedSource, (int Id, string Name)>()
                .IncludeMembers(source => source.Values);
            builder.Map<
                FlattenedSource,
                (int CustomerId, string CustomerName)>();
            builder.Map<(int X, int Y), (int Y, int X)>();
            builder.Map<
                (int Id, string Name, int Extra),
                (string Name, int Id)>();
            builder.Map<
                ((int X, int Y) Point, int Count),
                (int Count, (int X, int Y) Point)>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();

            var objectToTuple =
                (ITypeMapper<ObjectSource, (string Name, int Id)>)mapper;
            var createdTuple = objectToTuple.Create(
                new ObjectSource(17, "Ada", 99),
                default(MappingContext));
            var updatedTuple = objectToTuple.Update(
                new ObjectSource(23, "Grace", 100),
                (Name: "old", Id: -1),
                default(MappingContext));

            var tupleToObject =
                (ITypeMapper<(int Id, string Name), ObjectDestination>)mapper;
            var createdObject = tupleToObject.Create(
                (Id: 31, Name: "Linus"),
                default(MappingContext));
            var existingObject = new ObjectDestination
            {
                Id = -1,
                Name = "old"
            };
            var updatedObject = tupleToObject.Update(
                (Id: 37, Name: "Margaret"),
                existingObject,
                default(MappingContext));

            var tupleToRecord =
                (ITypeMapper<(int Id, string Name), RecordDestination>)mapper;
            var createdRecord = tupleToRecord.Create(
                (Id: 41, Name: "Barbara"),
                default(MappingContext));

            var included =
                (ITypeMapper<IncludedSource, (int Id, string Name)>)mapper;
            var includedResult = included.Create(
                new IncludedSource(new NestedValues(42, "included")),
                default(MappingContext));

            var flattened =
                (ITypeMapper<
                    FlattenedSource,
                    (int CustomerId, string CustomerName)>)mapper;
            var flattenedResult = flattened.Create(
                new FlattenedSource(new NestedValues(44, "flattened")),
                default(MappingContext));

            var reorder =
                (ITypeMapper<(int X, int Y), (int Y, int X)>)mapper;
            var reordered = reorder.Create(
                (X: 3, Y: 5),
                default(MappingContext));
            var reorderedUpdate = reorder.Update(
                (X: 7, Y: 11),
                (Y: -1, X: -2),
                default(MappingContext));

            var narrower =
                (ITypeMapper<
                    (int Id, string Name, int Extra),
                    (string Name, int Id)>)mapper;
            var narrowed = narrower.Create(
                (Id: 43, Name: "Edsger", Extra: 101),
                default(MappingContext));

            var recursive =
                (ITypeMapper<
                    ((int X, int Y) Point, int Count),
                    (int Count, (int X, int Y) Point)>)mapper;
            var recursiveResult = recursive.Create(
                (Point: (X: 47, Y: 53), Count: 59),
                default(MappingContext));

            if (createdTuple != ("Ada", 17) ||
                updatedTuple != ("Grace", 23) ||
                createdObject.Id != 31 ||
                createdObject.Name != "Linus" ||
                !ReferenceEquals(existingObject, updatedObject) ||
                updatedObject.Id != 37 ||
                updatedObject.Name != "Margaret" ||
                createdRecord.id != 41 ||
                createdRecord.name != "Barbara" ||
                includedResult != (42, "included") ||
                flattenedResult != (44, "flattened") ||
                reordered != (Y: 5, X: 3) ||
                reorderedUpdate != (Y: 11, X: 7) ||
                narrowed != ("Edsger", 43) ||
                recursiveResult != (59, (47, 53)))
            {
                throw new InvalidOperationException(
                    "Tuple name conventions produced an unexpected result.");
            }
        }
    }
}
