#nullable enable
#pragma warning disable CS1591
using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.StrictRuntimeTypes
{
    public readonly struct Number
    {
        public Number(int value) => Value = value;
        public int Value { get; }
    }
    public sealed record ReferenceNumber(int Value);
    public sealed class Destination
    {
        public int Value { get; set; }
    }
    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public int ArrayCalls { get; private set; }
        private int Count(object[]? source)
        {
            ArrayCalls++;
            return source is null ? -1 : source.Length;
        }
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Number?, Destination>().UnknownDerivedTypeHandling(UnknownDerivedTypeHandling.Throw);
            builder.Map<ReferenceNumber?, Destination>().UnknownDerivedTypeHandling(UnknownDerivedTypeHandling.Throw);
            builder.Map<object[], int>().UnknownDerivedTypeHandling(UnknownDerivedTypeHandling.Throw).Convert(Count);
        }
    }
    public static class Scenario
    {
        public static void VerifyExact(bool reference, bool nullSource, string operation)
        {
            var mapper = new TestMapper();
            var previous = operation == "Reuse" ? new Destination { Value = 40 } : null;
            Destination? result;
            if (reference)
            {
                ITypeMapper<ReferenceNumber?, Destination> contract = mapper;
                var source = nullSource ? null : new ReferenceNumber(7);
                result = operation == "Create" ? contract.Create(source) : contract.Update(source, previous);
            }
            else
            {
                ITypeMapper<Number?, Destination> contract = mapper;
                Number? source = nullSource ? (Number?)null : new Number(7);
                result = operation == "Create" ? contract.Create(source) : contract.Update(source, previous);
            }
            if (nullSource ? result is not null : result is null || result.Value != 7 ||
                ReferenceEquals(result, previous) != (previous is not null))
                throw new InvalidOperationException("Exact runtime type or null handling changed.");
            if (nullSource && previous is not null && previous.Value != 40)
                throw new InvalidOperationException("A null source mutated the destination.");
        }

        public static void VerifyArray(string kind, bool update)
        {
            var mapper = new TestMapper();
            ITypeMapper<object[], int> contract = mapper;
            object[]? source = kind == "Null" ? null : kind == "Derived" ? new string[2] : new object[2];
            bool threw = false;
            int result = 0;
            try { result = update ? contract.Update(source, 40) : contract.Create(source); }
            catch (UnmatchedPolymorphicMappingException error)
            {
                threw = true;
                if (error.Operation != (update ? MappingOperation.Update : MappingOperation.Create) ||
                    error.SourceType != typeof(object[]) || error.ActualSourceType != typeof(string[]))
                    throw new InvalidOperationException("Strict array dispatch lost exception details.");
            }
            if (threw != (kind == "Derived") || mapper.ArrayCalls != (threw ? 0 : 1) ||
                !threw && result != (kind == "Null" ? -1 : 2))
                throw new InvalidOperationException("Strict dispatch ignored array covariance or manual null behavior.");
        }
    }
}
