// Compiled integration scenario: tuple forms and boundaries
#nullable enable
#pragma warning disable CS1591

using System;
using System.Runtime.CompilerServices;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TupleShapes_a7b10004
{
    public sealed record ScalarSource(int Value);

    public sealed record UnnamedSource(int Value, string Text);

    public sealed record PartialSource(int Id, string Text);

    public sealed class LongSource
    {
        public int A { get; init; }
        public int B { get; init; }
        public int C { get; init; }
        public int D { get; init; }
        public int E { get; init; }
        public int F { get; init; }
        public int G { get; init; }
        public int H { get; init; }
        public int I { get; init; }
    }

    public sealed class LegacySource
    {
        public int A { get; init; }
        public int B { get; init; }
        public int C { get; init; }
        public int D { get; init; }
        public int E { get; init; }
        public int F { get; init; }
        public int G { get; init; }
        public int H { get; init; }
        public int I { get; init; }
    }

    public sealed class TechnicalDestination
    {
        public int Value { get; set; }

        public string Text { get; set; } = string.Empty;
    }

    public sealed record CustomSource(int Value);

    public sealed class CustomTuple : ITuple
    {
        public int Value { get; set; }

        int ITuple.Length => 1;

        object? ITuple.this[int index] => index == 0
            ? Value
            : throw new IndexOutOfRangeException();
    }

    public sealed class InterfaceDestination
    {
        public int Value { get; set; }
    }

    public sealed record NullableSource(int Id, string? Name);

    public sealed class NullableTupleSourceDestination
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    public sealed class MalformedSource
    {
        public int Item1 { get; init; }
        public int Item2 { get; init; }
        public int Item3 { get; init; }
        public int Item4 { get; init; }
        public int Item5 { get; init; }
        public int Item6 { get; init; }
        public int Item7 { get; init; }
        public DateTime Rest { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ScalarSource, ValueTuple>();
            builder.Map<ScalarSource, ValueTuple<int>>()
                .Members(source => new()
                {
                    Item1 = source.Value
                });
            builder.Map<ScalarSource, Tuple<int>>()
                .Members(source => new()
                {
                    Item1 = Ignore()
                });
            builder.Map<UnnamedSource, (int, string)>()
                .Members(source => new()
                {
                    Item1 = source.Value,
                    Item2 = source.Text
                });
            builder.Map<PartialSource, (int Id, string)>()
                .Members(source => new()
                {
                    Item2 = source.Text
                });
            builder.Map<
                LongSource,
                (
                    int A,
                    int B,
                    int C,
                    int D,
                    int E,
                    int F,
                    int G,
                    int H,
                    int I)>();
            builder.Map<
                LegacySource,
                Tuple<
                    int,
                    int,
                    int,
                    int,
                    int,
                    int,
                    int,
                    Tuple<int, int>>>()
                .Members(source => new()
                {
                    Item1 = source.A,
                    Item2 = source.B,
                    Item3 = source.C,
                    Item4 = source.D,
                    Item5 = source.E,
                    Item6 = source.F,
                    Item7 = source.G,
                    Item8 = source.H,
                    Item9 = source.I
                });
            builder.Map<Tuple<int, string>, TechnicalDestination>()
                .Members(source => new()
                {
                    Value = source.Item1,
                    Text = source.Item2
                });
            builder.Map<CustomSource, CustomTuple>();
            builder.Map<ITuple, InterfaceDestination>()
                .Members(source => new()
                {
                    Value = (int)source[0]!
                });
            builder.Map<NullableSource, (int Id, string? Name)?>();
            builder.Map<
                (int Id, string Name)?,
                NullableTupleSourceDestination>();
            builder.Map<
                MalformedSource,
                ValueTuple<
                    int,
                    int,
                    int,
                    int,
                    int,
                    int,
                    int,
                    DateTime>>()
                .ConstructorSelection(ConstructorSelection.Parameterless);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();

            VerifyEmptyAndSingleton(mapper);
            VerifyUnnamedAndPartiallyNamed(mapper);
            VerifyLongTuples(mapper);
            VerifyStaticITupleBoundaries(mapper);
            VerifyNullableTuple(mapper);
            VerifyMalformedRest(mapper);
        }

        private static void VerifyEmptyAndSingleton(TestMapper mapper)
        {
            var empty =
                (ITypeMapper<ScalarSource, ValueTuple>)mapper;
            var emptyResult = empty.Create(
                new ScalarSource(3),
                default(MappingContext));

            var singleton =
                (ITypeMapper<ScalarSource, ValueTuple<int>>)mapper;
            var singletonResult = singleton.Create(
                new ScalarSource(5),
                default(MappingContext));
            var singletonUpdate = singleton.Update(
                new ScalarSource(7),
                new ValueTuple<int>(-1),
                default(MappingContext));
            var ignored =
                (ITypeMapper<ScalarSource, Tuple<int>>)mapper;
            var ignoredResult = ignored.Create(
                new ScalarSource(9),
                default(MappingContext));

            if (!emptyResult.Equals(default(ValueTuple)) ||
                singletonResult.Item1 != 5 ||
                singletonUpdate.Item1 != 7 ||
                ignoredResult.Item1 != 0)
            {
                throw new InvalidOperationException(
                    "Empty or singleton ValueTuple mapping failed.");
            }
        }

        private static void VerifyUnnamedAndPartiallyNamed(TestMapper mapper)
        {
            var unnamed =
                (ITypeMapper<UnnamedSource, (int, string)>)mapper;
            var unnamedResult = unnamed.Create(
                new UnnamedSource(11, "explicit"),
                default(MappingContext));

            var partial =
                (ITypeMapper<PartialSource, (int Id, string)>)mapper;
            var partialResult = partial.Create(
                new PartialSource(13, "partial"),
                default(MappingContext));

            if (unnamedResult != (11, "explicit") ||
                partialResult != (13, "partial"))
            {
                throw new InvalidOperationException(
                    "Explicit unnamed tuple element mapping failed.");
            }
        }

        private static void VerifyLongTuples(TestMapper mapper)
        {
            var valueContract =
                (ITypeMapper<
                    LongSource,
                    (
                        int A,
                        int B,
                        int C,
                        int D,
                        int E,
                        int F,
                        int G,
                        int H,
                        int I)>)mapper;
            var value = valueContract.Create(
                new LongSource
                {
                    A = 1,
                    B = 2,
                    C = 3,
                    D = 4,
                    E = 5,
                    F = 6,
                    G = 7,
                    H = 8,
                    I = 9
                },
                default(MappingContext));

            var legacyContract =
                (ITypeMapper<
                    LegacySource,
                    Tuple<
                        int,
                        int,
                        int,
                        int,
                        int,
                        int,
                        int,
                        Tuple<int, int>>>)mapper;
            var legacy = legacyContract.Create(
                new LegacySource
                {
                    A = 11,
                    B = 12,
                    C = 13,
                    D = 14,
                    E = 15,
                    F = 16,
                    G = 17,
                    H = 18,
                    I = 19
                },
                default(MappingContext));

            if (value.A != 1 || value.G != 7 || value.H != 8 || value.I != 9 ||
                legacy.Item1 != 11 ||
                legacy.Item7 != 17 ||
                legacy.Rest.Item1 != 18 ||
                legacy.Rest.Item2 != 19)
            {
                throw new InvalidOperationException(
                    "Long tuple lowering exposed or misplaced Rest.");
            }
        }

        private static void VerifyStaticITupleBoundaries(TestMapper mapper)
        {
            var technical =
                (ITypeMapper<Tuple<int, string>, TechnicalDestination>)mapper;
            var technicalResult = technical.Create(
                new Tuple<int, string>(23, "legacy"),
                default(MappingContext));

            var custom =
                (ITypeMapper<CustomSource, CustomTuple>)mapper;
            var customResult = custom.Create(
                new CustomSource(29),
                default(MappingContext));

            var interfaceContract =
                (ITypeMapper<ITuple, InterfaceDestination>)mapper;
            var interfaceResult = interfaceContract.Create(
                customResult,
                default(MappingContext));

            if (technicalResult.Value != 23 ||
                technicalResult.Text != "legacy" ||
                customResult.Value != 29 ||
                interfaceResult.Value != 29)
            {
                throw new InvalidOperationException(
                    "Custom or interface ITuple used a runtime convention.");
            }
        }

        private static void VerifyNullableTuple(TestMapper mapper)
        {
            var contract =
                (ITypeMapper<NullableSource, (int Id, string? Name)?>)mapper;
            var created = contract.Create(
                new NullableSource(31, null),
                default(MappingContext));
            var updated = contract.Update(
                new NullableSource(37, "nullable"),
                null,
                default(MappingContext));
            var sourceContract =
                (ITypeMapper<
                    (int Id, string Name)?,
                    NullableTupleSourceDestination>)mapper;
            var sourceCreated = sourceContract.Create(
                (Id: 41, Name: "source"),
                default(MappingContext));
            var nullSourceResult = sourceContract.Create(
                null,
                default(MappingContext));

            if (!created.HasValue ||
                created.Value.Id != 31 ||
                created.Value.Name is not null ||
                !updated.HasValue ||
                updated.Value.Id != 37 ||
                updated.Value.Name != "nullable" ||
                sourceCreated.Id != 41 ||
                sourceCreated.Name != "source" ||
                nullSourceResult is not null)
            {
                throw new InvalidOperationException(
                    "Nullable ValueTuple root mapping failed.");
            }
        }

        private static void VerifyMalformedRest(TestMapper mapper)
        {
            var contract =
                (ITypeMapper<
                    MalformedSource,
                    ValueTuple<
                        int,
                        int,
                        int,
                        int,
                        int,
                        int,
                        int,
                        DateTime>>)mapper;
            var rest = new DateTime(2026, 8, 26);
            var result = contract.Create(
                new MalformedSource
                {
                    Item1 = 1,
                    Item2 = 2,
                    Item3 = 3,
                    Item4 = 4,
                    Item5 = 5,
                    Item6 = 6,
                    Item7 = 7,
                    Rest = rest
                },
                default(MappingContext));

            if (result.Item1 != 1 ||
                result.Item7 != 7 ||
                result.Rest != rest)
            {
                throw new InvalidOperationException(
                    "A malformed Rest chain was flattened as a tuple.");
            }
        }
    }
}
