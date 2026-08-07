// Compiled integration scenario: TypeMapperDeclarativeControlFlowTests/ExpressionTests::Executes_plan_and_marker_conditional_and_switch_expressions
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using Morphant.Members;
using Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Expression_2c95c8b8.Morphant.Generated;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Expression_2c95c8b8
{
    public sealed class Source
    {
        public int Value { get; init; }

        public int Automatic { get; init; }

        public int Rule { get; init; }

        public bool Left { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }

        public int Automatic { get; set; }

        public string Path { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, previous) =>
                {
                    Member<int> value = source.Rule switch
                    {
                        0 => Auto(),
                        1 => Ignore(),
                        _ => source.Value * 3
                    };
                    var plan = source.Left
                        ? new DestinationMembers
                        {
                            Value = value,
                            Automatic = source.Automatic,
                            Path = previous.HasValue
                                ? "left-update"
                                : "left-create"
                        }
                        : new DestinationMembers
                        {
                            Value = value,
                            Automatic = source.Automatic,
                            Path = "right"
                        };

                    return plan;
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var context = default(MappingContext);
            var automatic = mapper.Create(
                new Source
                {
                    Value = 2,
                    Automatic = 11,
                    Rule = 0,
                    Left = true
                },
                context);
            var previous = new Destination
            {
                Value = 17,
                Automatic = 1
            };
            var ignored = mapper.Update(
                new Source
                {
                    Value = 3,
                    Automatic = 12,
                    Rule = 1,
                    Left = false
                },
                previous,
                context);
            var explicitValue = mapper.Create(
                new Source
                {
                    Value = 4,
                    Automatic = 13,
                    Rule = 2,
                    Left = false
                },
                context);

            if (automatic.Value != 2 ||
                automatic.Automatic != 11 ||
                automatic.Path != "left-create" ||
                !ReferenceEquals(previous, ignored) ||
                ignored.Value != 17 ||
                ignored.Automatic != 12 ||
                ignored.Path != "right" ||
                explicitValue.Value != 12 ||
                explicitValue.Automatic != 13 ||
                explicitValue.Path != "right")
            {
                throw new InvalidOperationException(
                    "Declarative expressions were lowered incorrectly.");
            }
        }
    }
}
