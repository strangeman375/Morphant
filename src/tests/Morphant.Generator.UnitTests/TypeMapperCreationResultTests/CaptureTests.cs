using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperCreationResultTests;

[TestFixture]
internal sealed class CaptureTests
{
    [Test]
    public void Rejects_runtime_Configure_locals_for_direct_and_factory_code()
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

    public sealed class Destination
    {
        public Destination(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var offset = Environment.TickCount;

            builder.Map<Source, int>()
                .Construct(source => source.Value + offset);

            builder.Map<Source, Destination>()
                .Construct(source => new(ByFactory(() =>
                    new Destination(source.Value + offset))));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 3 };
            var context = default(MappingContext);

            AssertUnsupported(() =>
                ((ITypeMapper<Source, int>)mapper)
                .Create(source, context));
            AssertUnsupported(() =>
                ((ITypeMapper<Source, Destination>)mapper)
                .Create(source, context));
        }

        private static void AssertUnsupported(Action action)
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
                "A Configure-local capture escaped into generated code.");
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
    public void Requires_direct_Construct_only_for_reachable_creation()
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

    public interface IDestination
    {
        int Value { get; set; }
    }

    public sealed class Destination : IDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, IDestination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, IDestination>)new TestMapper();
            var source = new Source { Value = 7 };
            var context = default(MappingContext);
            var previous = new Destination();
            var updated = mapper.Update(source, previous, context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Value != 7)
            {
                throw new InvalidOperationException(
                    "Existing direct destination was not mapped.");
            }

            try
            {
                mapper.Create(source, context);
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Direct creation silently used automatic construction.");
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
