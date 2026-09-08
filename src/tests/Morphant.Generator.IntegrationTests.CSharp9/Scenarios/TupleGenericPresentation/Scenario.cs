#nullable enable
using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TupleGenericPresentation
{
    public sealed class Envelope<T> where T : class
    {
        public T? Value { get; set; }
        public int Count { get; set; }
        public dynamic State { get; set; } = new State();
    }
    public sealed class State { public int Add(int value) => value + 5; }

    public abstract class Family<TMapper, T> : TypeMapper<TMapper>
        where TMapper : Family<TMapper, T> where T : class
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Envelope<T>, ((T? Value, int Count) Inner, dynamic State)?>()
                .Members(source => new() { Inner = (source.Value, source.Count), State = source.State });
    }
    [MorphantMapper]
    public partial class TestMapper : Family<TestMapper, string>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Envelope<string>, ((string? Value, int Count) Inner, dynamic State)?>()
                .IncludeBase<Envelope<string>, ((string? Value, int Count) Inner, dynamic State)?>();
        }
    }
    public static class Scenario
    {
        public static void Verify(string operation, bool nullElement)
        {
            ITypeMapper<Envelope<string>, ((string? Value, int Count) Inner, dynamic State)?> mapper = new TestMapper();
            var state = new State();
            var source = new Envelope<string> { Value = nullElement ? null : "mapped", Count = 31, State = state };
            var result = operation switch
            {
                "Create" => mapper.Create(source),
                "UpdateNull" => mapper.Update(source, null),
                "UpdateExisting" => mapper.Update(source, (("old", -1), new State())),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            if (!result.HasValue || result.Value.Inner.Value != (nullElement ? null : "mapped") ||
                result.Value.Inner.Count != 31 || !ReferenceEquals((object)result.Value.State, state) ||
                (int)result.Value.State.Add(7) != 12)
                throw new InvalidOperationException("Closing a tuple family must preserve nested names, null elements, and dynamic operations.");
        }
    }
}
