// Compiled integration scenario: TypeMapperDeclarativeControlFlowTests/ThrowTests::Preserves_throw_expression_and_non_exhaustive_switch_fallback
#nullable enable
#pragma warning disable CS1591
#pragma warning disable CS8509

using Morphant;
using Morphant.Context;
using System;
using System.Runtime.CompilerServices;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Throw_8f7cf658
{
    public sealed class Source
    {
        public int Mode { get; init; }
    }

    public sealed class Destination
    {
        public string Path { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int SelectorCount { get; private set; }

        public static int ThrowCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) =>
                    Select(source.Mode) switch
                    {
                        0 => new() { Path = "mapped" },
                        1 => throw BuildFailure()
                    });

        private static int Select(int value)
        {
            SelectorCount++;
            return value;
        }

        private static Exception BuildFailure()
        {
            ThrowCount++;
            return new InvalidOperationException("explicit");
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var context = default(MappingContext);
            var mapped = mapper.Create(
                new Source { Mode = 0 },
                context);

            if (mapped.Path != "mapped" ||
                TestMapper.SelectorCount != 1 ||
                TestMapper.ThrowCount != 0)
            {
                throw new InvalidOperationException(
                    "The selected non-throw path was not preserved.");
            }

            try
            {
                mapper.Create(new Source { Mode = 1 }, context);
            }
            catch (InvalidOperationException exception)
                when (exception.Message == "explicit" &&
                      TestMapper.SelectorCount == 2 &&
                      TestMapper.ThrowCount == 1)
            {
            }

            try
            {
                mapper.Create(new Source { Mode = 2 }, context);
            }
            catch (SwitchExpressionException exception)
                when ((int?)exception.UnmatchedValue == 2 &&
                      TestMapper.SelectorCount == 3 &&
                      TestMapper.ThrowCount == 1)
            {
                return;
            }

            throw new InvalidOperationException(
                "The declarative throw path was not preserved.");
        }
    }
}
