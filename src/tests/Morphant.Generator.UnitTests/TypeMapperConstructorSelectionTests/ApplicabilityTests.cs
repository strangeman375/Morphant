using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class ApplicabilityTests
{
    [Test]
    public void Ignores_inherited_values_for_direct_and_manual_mappings()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class ManualDestination
    {
        public ManualDestination(int value) => Value = value;

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.ConstructorSelection(ConstructorSelection.Explicit);

            builder.Map<Source, string>()
                .Construct(source => source.Value.ToString());
            builder.Map<Source, ManualDestination>()
                .Convert((source, _, _) =>
                    new ManualDestination(source?.Value ?? -1));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 17 };
            var context = default(MappingContext);
            var direct =
                ((ITypeMapper<Source, string>)mapper)
                    .Create(source, context);
            var manual =
                ((ITypeMapper<Source, ManualDestination>)mapper)
                    .Create(source, context);

            if (direct != "17" || manual.Value != 17)
            {
                throw new InvalidOperationException(
                    "Inherited ConstructorSelection affected an inapplicable mapping.");
            }
        }
    }
}
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Preserves_explicit_map_level_values_as_invalid_state()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class ManualDestination
    {
        public ManualDestination(int value) => Value = value;

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, string>()
                .ConstructorSelection(ConstructorSelection.Default)
                .Construct(source => source.Value.ToString());
            builder.Map<Source, ManualDestination>()
                .ConstructorSelection(ConstructorSelection.Default)
                .Convert((source, _, _) =>
                    new ManualDestination(source?.Value ?? -1));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 17 };
            var context = default(MappingContext);

            ExpectNotSupported(() =>
                ((ITypeMapper<Source, string>)mapper)
                    .Create(source, context));
            ExpectNotSupported(() =>
                ((ITypeMapper<Source, ManualDestination>)mapper)
                    .Create(source, context));
        }

        private static void ExpectNotSupported(Action action)
        {
            try
            {
                action();
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "An inapplicable map-level ConstructorSelection was ignored.");
        }
    }
}
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
