#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TupleGenericInheritance
{
    public sealed class Source<T> where T : class
    {
        public (T? Value, int Count) Data { get; init; }
    }

    public abstract class ConstructionFamily<TMapper, T> : TypeMapper<TMapper>
        where TMapper : ConstructionFamily<TMapper, T>
        where T : class
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, (T? Value, int Count)>()
                .Construct(source => new(
                    Value: source.Data.Value,
                    Count: source.Data.Count));
    }

    public abstract class MemberFamily<TMapper, T> : ConstructionFamily<TMapper, T>
        where TMapper : MemberFamily<TMapper, T>
        where T : class
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source<T>, (T? Value, int Count)>()
                .IncludeBase<Source<T>, (T? Value, int Count)>()
                .Members(source => new() { Count = source.Data.Count + 10 });
        }
    }

    [MorphantMapper]
    public partial class TestMapper : MemberFamily<TestMapper, string>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source<string>, (string? Value, int Count)>()
                .IncludeBase<Source<string>, (string? Value, int Count)>()
                .Members(source => new()
                {
                    Value = source.Data.Value == null ? null : source.Data.Value.ToUpperInvariant()
                });
        }
    }

    public abstract class ConversionFamily<TMapper, T> : TypeMapper<TMapper>
        where TMapper : ConversionFamily<TMapper, T>
        where T : class
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source<T>, (T? Value, int Count)>()
                .Convert(source => (source!.Data.Value, source.Data.Item2 + 1));
    }

    [MorphantMapper]
    public partial class ConversionMapper : ConversionFamily<ConversionMapper, string>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source<string>, (string? Value, int Count)>()
                .IncludeBase<Source<string>, (string? Value, int Count)>();
        }
    }

    public sealed class LongSource<T> where T : class
    {
        public (int A, int B, int C, int D, int E, int F, int G,
            (T? Value, int Count)? Tail) Data { get; init; }
    }

    public abstract class LongFamily<TMapper, T> : TypeMapper<TMapper>
        where TMapper : LongFamily<TMapper, T>
        where T : class
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<LongSource<T>, (T? Value, int Count)>()
                .Construct(source => new(
                    Value: source.Data.Tail?.Value,
                    Count: source.Data.Item8?.Item2 ?? 0));
    }

    [MorphantMapper]
    public partial class LongMapper : LongFamily<LongMapper, string>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<LongSource<string>, (string? Value, int Count)>()
                .IncludeBase<LongSource<string>, (string? Value, int Count)>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source<string>, (string? Value, int Count)>)new TestMapper();
            var created = mapper.Create(new Source<string> { Data = ("tuple", 7) });
            var previous = (Value: (string?)"previous", Count: -1);
            var updated = mapper.Update(new Source<string> { Data = (null, 23) }, previous);
            var empty = mapper.Create(new Source<string> { Data = (null, 0) });

            if (created != ("TUPLE", 17) || updated != (null, 33) ||
                empty != (null, 10) || previous != ("previous", -1))
                throw new InvalidOperationException(
                    "Generic IncludeBase lost tuple names, nullable values, or inherited and local rules.");
        }

        public static void VerifyConversion()
        {
            var mapper = (ITypeMapper<Source<string>, (string? Value, int Count)>)new ConversionMapper();
            var created = mapper.Create(new Source<string> { Data = ("tuple", 7) });
            var previous = (Value: (string?)"previous", Count: -1);
            var updated = mapper.Update(new Source<string> { Data = (null, 23) }, previous);

            if (created != ("tuple", 8) || updated != (null, 24) || previous != ("previous", -1))
                throw new InvalidOperationException("Inherited Convert lost named or positional tuple field values.");
        }

        public static void VerifyLongTuple(bool hasTail)
        {
            var mapper = (ITypeMapper<LongSource<string>, (string? Value, int Count)>)new LongMapper();
            (string? Value, int Count)? tail = hasTail ? ("tail", 17) : null;
            var result = mapper.Create(new LongSource<string> { Data = (1, 2, 3, 4, 5, 6, 7, tail) });

            if (result != (hasTail ? "tail" : null, hasTail ? 17 : 0))
                throw new InvalidOperationException("Inherited construction lost a nested nullable element beyond Item7.");
        }
    }
}
