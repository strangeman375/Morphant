// Compiled integration scenario: TypeMapperMemberTests/MemberSelectionTests::Preserves_an_invalid_effective_MemberSelection_as_a_complete_stub
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0021

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InvalidMemberSelection_5d3e2b8a
{
    public readonly struct Source
    {
        public int Value { get; init; }
    }

    public struct Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection((MemberSelection)int.MaxValue);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var context = default(MappingContext);
            var source = new Source { Value = 17 };

            ExpectInvalid(() => mapper.Create(source, context));
            ExpectInvalid(() => mapper.Update(
                source,
                new Destination { Value = 41 },
                context));
        }

        private static void ExpectInvalid(Action action)
        {
            try
            {
                action();
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException
                   exception)
                when (exception.Message.Contains(
                    "MemberSelection has an invalid value.",
                    StringComparison.Ordinal))
            {
                return;
            }

            throw new InvalidOperationException(
                "An invalid MemberSelection did not preserve a complete " +
                "failure contract.");
        }
    }
}
