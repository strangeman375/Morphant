// Compiled integration scenario: TypeMapperConstructorSelectionTests/GreediestAndLargestTests::Greediest_excludes_nullable_warning_and_required_member_failures
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.GreediestAndLargest_22a7d3d6
{
    public sealed class NullableSource
    {
        public int Id { get; init; }

        public string? Name { get; init; }
    }

    public sealed class NullableDestination
    {
        public NullableDestination(int id)
        {
            Kind = "safe";
            Value = id;
        }

        public NullableDestination(string name, int code = 0)
        {
            Kind = name;
            Value = code;
        }

        public string Kind { get; }

        public int Value { get; }
    }

    public sealed class RequiredSource
    {
        public int Id { get; init; }
    }

    public sealed class RequiredDestination
    {
        [SetsRequiredMembers]
        public RequiredDestination()
        {
            Kind = "sets-required";
            Token = "initialized";
        }

        public RequiredDestination(int id)
        {
            Kind = id.ToString();
        }

        public required string Token { get; init; }

        public string Kind { get; }
    }

    public sealed class MappedRequiredSource
    {
        public int Id { get; init; }

        public string Token { get; init; } = string.Empty;
    }

    public sealed class MappedRequiredDestination
    {
        public MappedRequiredDestination()
        {
            Kind = "parameterless";
        }

        public MappedRequiredDestination(int id)
        {
            Kind = id.ToString();
        }

        public required string Token { get; init; }

        public string Kind { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<NullableSource, NullableDestination>()
                .ConstructorSelection(ConstructorSelection.Greediest);
            builder.Map<RequiredSource, RequiredDestination>()
                .ConstructorSelection(ConstructorSelection.Greediest);
            builder.Map<MappedRequiredSource, MappedRequiredDestination>()
                .ConstructorSelection(ConstructorSelection.Greediest);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var nullable =
                ((ITypeMapper<NullableSource, NullableDestination>)mapper)
                    .Create(
                        new NullableSource { Id = 17, Name = null },
                        context);
            var required =
                ((ITypeMapper<RequiredSource, RequiredDestination>)mapper)
                    .Create(new RequiredSource { Id = 31 }, context);
            var mappedRequired =
                ((ITypeMapper<MappedRequiredSource, MappedRequiredDestination>)mapper)
                    .Create(
                        new MappedRequiredSource
                        {
                            Id = 47,
                            Token = "mapped"
                        },
                        context);

            if (nullable.Kind != "safe" ||
                nullable.Value != 17 ||
                required.Kind != "sets-required" ||
                required.Token != "initialized" ||
                mappedRequired.Kind != "47" ||
                mappedRequired.Token != "mapped")
            {
                throw new InvalidOperationException(
                    "Greediest ignored warning-free or required-member applicability.");
            }
        }
    }
}
