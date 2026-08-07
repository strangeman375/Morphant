using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperConventionTests;

[TestFixture]
internal sealed class ConstructorTests
{
    [Test]
    public void Omits_an_unmatched_optional_constructor_parameter()
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
        public int Id { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int id, string label = "fallback")
        {
            Id = id;
            Label = label;
        }

        public int Id { get; }

        public string Label { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
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
                    Id = 83
                },
                default(MappingContext));

            if (result.Id != 83 || result.Label != "fallback")
            {
                throw new InvalidOperationException(
                    "An optional constructor parameter was not omitted.");
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
    public void Does_not_use_mapper_lexical_access_to_a_private_constructor()
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

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public sealed class Destination
        {
            private Destination()
            {
            }

            public int Value { get; set; }

            public static Destination Existing() => new();
        }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, TestMapper.Destination>)
                new TestMapper();
            var previous = TestMapper.Destination.Existing();
            var updated = mapper.Update(
                new Source
                {
                    Value = 67
                },
                previous,
                default(MappingContext));

            if (!ReferenceEquals(updated, previous) || updated.Value != 67)
            {
                throw new InvalidOperationException(
                    "Update through an assembly-stable surface failed.");
            }

            try
            {
                _ = mapper.Create(
                    new Source(),
                    default(MappingContext));
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Mapper lexical access leaked into constructor selection.");
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
    public void Evaluates_a_shared_constructor_and_required_member_value_once()
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
        private int reads;

        public int Value
        {
            get
            {
                reads++;
                return 61;
            }
        }

        public int Reads => reads;
    }

    public sealed class Destination
    {
        public Destination(int value)
        {
            ConstructorValue = value;
        }

        public int ConstructorValue { get; }

        public required int Value { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
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
            var source = new Source();
            var result = mapper.Create(source, default(MappingContext));

            if (result.ConstructorValue != 61 ||
                result.Value != 61 ||
                source.Reads != 1)
            {
                throw new InvalidOperationException(
                    "A shared convention value was evaluated more than once.");
            }
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp11,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Honors_SetsRequiredMembers_without_synthesizing_a_required_assignment()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;
using System.Diagnostics.CodeAnalysis;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        [SetsRequiredMembers]
        internal Destination()
        {
            Name = "constructor";
        }

        public required string Name { get; set; }

        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
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
                    Value = 59
                },
                default(MappingContext));

            if (result.Name != "constructor" || result.Value != 59)
            {
                throw new InvalidOperationException(
                    "SetsRequiredMembers was not honored.");
            }
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp11,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Selects_the_only_parameterized_constructor_and_maps_its_arguments()
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
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public Destination()
        {
            Id = -1;
            Name = "parameterless";
        }

        internal Destination(int id, string name = "optional")
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }

        public string Name { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
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
                    Id = 23,
                    Name = "parameterized"
                },
                default(MappingContext));

            if (result.Id != 23 || result.Name != "parameterized")
            {
                throw new InvalidOperationException(
                    "The unambiguous constructor was not used.");
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
    public void Does_not_fallback_from_an_ambiguous_constructor_selection()
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
        public Destination()
        {
        }

        public Destination(int value)
        {
            Value = value;
        }

        public Destination(long value)
        {
            Value = (int)value;
        }

        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
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
            var previous = new Destination
            {
                Value = 1
            };
            var updated = mapper.Update(
                new Source
                {
                    Value = 9
                },
                previous,
                default(MappingContext));

            if (!ReferenceEquals(updated, previous) || updated.Value != 9)
            {
                throw new InvalidOperationException(
                    "Update must remain available without construction.");
            }

            try
            {
                _ = mapper.Create(
                    new Source(),
                    default(MappingContext));
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Ambiguous convention construction unexpectedly fell back.");
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
