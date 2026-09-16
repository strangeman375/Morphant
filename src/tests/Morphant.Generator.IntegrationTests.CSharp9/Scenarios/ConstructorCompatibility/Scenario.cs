#nullable enable
#pragma warning disable CS1591
using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConstructorCompatibility
{
    public sealed class Customer
    {
        public int Id { get; init; }
        public string? Name { get; init; }
    }

    public sealed class Source
    {
        public Customer Customer { get; init; } = new();
        public int Code { get; init; }
        public string? Text { get; init; }
        public string? Optional { get; init; }
    }

    public sealed class Destination
    {
        public Destination(string text) => throw new InvalidOperationException("Nullable direct argument selected.");
        public Destination(string customerName, bool marker = false) =>
            throw new InvalidOperationException("Nullable flattened argument selected.");
        public Destination(int customerId, int code, string optional = "fallback")
        {
            Value = customerId + code;
            Optional = optional;
        }

        public int Value { get; }
        public string Optional { get; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructorSelection(ConstructorSelection.Greediest);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source { Customer = new Customer { Id = 17 }, Code = 5 };
            var created = mapper.Create(source, default(MappingContext));
            var missing = mapper.Update(source, null, default(MappingContext));
            var existing = mapper.Update(source, created, default(MappingContext));
            if (created.Value != 22 || created.Optional != "fallback" ||
                missing.Value != 22 || missing.Optional != "fallback" || !ReferenceEquals(created, existing))
                throw new InvalidOperationException("Constructor candidate binding or optional omission changed.");
        }
    }
}
