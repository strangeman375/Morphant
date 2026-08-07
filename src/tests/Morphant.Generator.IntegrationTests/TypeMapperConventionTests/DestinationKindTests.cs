using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperConventionTests;

[TestFixture]
internal sealed class DestinationKindTests
{
    [Test]
    public void Supports_record_and_constructed_generic_destinations()
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
    public sealed class NumberSource
    {
        public int Value { get; init; }
    }

    public sealed class TextSource
    {
        public string Value { get; init; } = string.Empty;
    }

    public sealed record RecordDestination
    {
        public int Value { get; set; }
    }

    public sealed class Box<T>
    {
        public T Value { get; set; } = default!;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<NumberSource, RecordDestination>();
            builder.Map<TextSource, Box<string>>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var recordMapper =
                (ITypeMapper<NumberSource, RecordDestination>)mapper;
            var boxMapper =
                (ITypeMapper<TextSource, Box<string>>)mapper;
            var record = recordMapper.Create(
                new NumberSource
                {
                    Value = 53
                },
                default(MappingContext));
            var box = boxMapper.Create(
                new TextSource
                {
                    Value = "generic"
                },
                default(MappingContext));

            if (record.Value != 53 || box.Value != "generic")
            {
                throw new InvalidOperationException(
                    "A nominal destination kind was not mapped.");
            }
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Supports_value_and_nullable_value_destination_lifecycles()
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

    public struct Destination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>();
            builder.Map<Source, Destination?>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source
            {
                Value = 29
            };
            var valueMapper =
                (ITypeMapper<Source, Destination>)mapper;
            var nullableMapper =
                (ITypeMapper<Source, Destination?>)mapper;
            var previous = new Destination
            {
                Value = 3
            };
            var created = valueMapper.Create(
                source,
                default(MappingContext));
            var updated = valueMapper.Update(
                source,
                previous,
                default(MappingContext));
            var nullableCreated = nullableMapper.Create(
                source,
                default(MappingContext));
            var nullableUpdated = nullableMapper.Update(
                source,
                previous,
                default(MappingContext));

            if (created.Value != 29 ||
                updated.Value != 29 ||
                previous.Value != 3 ||
                nullableCreated?.Value != 29 ||
                nullableUpdated?.Value != 29)
            {
                throw new InvalidOperationException(
                    "Value destination lifecycle produced an unexpected result.");
            }
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Updates_direct_abstract_and_interface_destinations_without_a_create_fallback()
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

    public interface IInterfaceDestination
    {
        int Value { get; set; }
    }

    public abstract class AbstractDestination
    {
        public int Value { get; set; }
    }

    public sealed class ConcreteDestination :
        AbstractDestination,
        IInterfaceDestination
    {
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, int>();
            builder.Map<Source, IInterfaceDestination>();
            builder.Map<Source, AbstractDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source
            {
                Value = 47
            };
            var scalarMapper = (ITypeMapper<Source, int>)mapper;
            var interfaceMapper =
                (ITypeMapper<Source, IInterfaceDestination>)mapper;
            var abstractMapper =
                (ITypeMapper<Source, AbstractDestination>)mapper;
            var interfacePrevious = new ConcreteDestination();
            var abstractPrevious = new ConcreteDestination();

            if (scalarMapper.Update(
                    source,
                    13,
                    default(MappingContext)) != 13 ||
                !ReferenceEquals(
                    interfaceMapper.Update(
                        source,
                        interfacePrevious,
                        default(MappingContext)),
                    interfacePrevious) ||
                interfacePrevious.Value != 47 ||
                !ReferenceEquals(
                    abstractMapper.Update(
                        source,
                        abstractPrevious,
                        default(MappingContext)),
                    abstractPrevious) ||
                abstractPrevious.Value != 47)
            {
                throw new InvalidOperationException(
                    "A direct destination Update was not authoritative.");
            }

            try
            {
                _ = scalarMapper.Create(
                    source,
                    default(MappingContext));
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A direct destination unexpectedly used automatic construction.");
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
