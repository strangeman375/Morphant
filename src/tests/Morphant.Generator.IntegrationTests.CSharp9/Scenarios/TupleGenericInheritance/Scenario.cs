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
    }
}
