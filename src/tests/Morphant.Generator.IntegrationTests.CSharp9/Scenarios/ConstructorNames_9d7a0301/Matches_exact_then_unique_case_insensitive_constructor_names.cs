// Compiled integration scenario: TypeMapperConventionTests/ConstructorTests::Matches_exact_then_unique_case_insensitive_constructor_names
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConstructorNames_9d7a0301
{
    public sealed class Source
    {
        public int Id { get; init; }

        public int label { get; init; }

        public int name { get; init; }

        public int NAME { get; init; }

        public int fieldValue;
    }

    public sealed class Destination
    {
        public Destination(
            int Id,
            int Label,
            int FieldValue,
            int Name = 97,
            int Missing = 101)
        {
            Exact = Id;
            CaseInsensitive = Label;
            SourceField = FieldValue;
            Ambiguous = Name;
            Optional = Missing;
        }

        public int Exact { get; }

        public int CaseInsensitive { get; }

        public int SourceField { get; }

        public int Ambiguous { get; }

        public int Optional { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var result = mapper.Create(
                new Source
                {
                    Id = 11,
                    label = 13,
                    name = 17,
                    NAME = 19,
                    fieldValue = 23
                },
                default(MappingContext));

            if (result.Exact != 11 ||
                result.CaseInsensitive != 13 ||
                result.SourceField != 23 ||
                result.Ambiguous != 97 ||
                result.Optional != 101)
            {
                throw new InvalidOperationException(
                    "Constructor convention did not prefer an exact name, " +
                    "accept a unique case-insensitive name, and omit an " +
                    "ambiguous or missing optional argument.");
            }
        }
    }
}
