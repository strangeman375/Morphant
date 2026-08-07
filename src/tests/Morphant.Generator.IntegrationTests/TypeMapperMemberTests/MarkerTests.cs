using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperMemberTests;

[TestFixture]
internal sealed class MarkerTests
{
    [Test]
    public void Keeps_an_unavailable_Auto_rule_as_an_unsupported_path()
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
        public object Value { get; init; } = new();
    }

    public sealed class Destination
    {
        public string Value { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((_, _) => new()
                {
                    Value = Auto()
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();

            try
            {
                mapper.Create(
                    new Source(),
                    default(MappingContext));
                throw new InvalidOperationException(
                    "An unavailable Auto rule was silently ignored.");
            }
            catch (NotSupportedException exception)
                when (exception.Message ==
                    "A configured Auto member cannot be mapped by convention.")
            {
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
    public void Applies_typed_and_target_typed_Auto_and_Ignore_markers()
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
        public int Automatic { get; init; }

        public int TypedAutomatic { get; init; }

        public string Ignored { get; init; } = string.Empty;

        public string TypedIgnored { get; init; } = string.Empty;

        public string Explicit { get; init; } = string.Empty;

        public string Convention { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public Destination()
        {
            Ignored = "created-ignore";
            TypedIgnored = "created-typed-ignore";
        }

        public int Automatic { get; set; }

        public int TypedAutomatic { get; set; }

        public string Ignored { get; set; }

        public string TypedIgnored { get; set; }

        public string Explicit { get; set; } = string.Empty;

        public string Convention { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((source, _) => new()
                {
                    Automatic = Auto(),
                    TypedAutomatic = Auto<int>(),
                    Ignored = Ignore(),
                    TypedIgnored = Ignore<string>(),
                    Explicit = source.Explicit + "!"
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = (ITypeMapper<Source, Destination>)
                new TestMapper();
            var context = default(MappingContext);
            var source = new Source
            {
                Automatic = 1,
                TypedAutomatic = 2,
                Ignored = "source-ignore",
                TypedIgnored = "source-typed-ignore",
                Explicit = "explicit",
                Convention = "convention"
            };
            var created = mapper.Create(source, context);

            if (created.Automatic != 1 ||
                created.TypedAutomatic != 2 ||
                created.Ignored != "created-ignore" ||
                created.TypedIgnored != "created-typed-ignore" ||
                created.Explicit != "explicit!" ||
                created.Convention != "convention")
            {
                throw new InvalidOperationException(
                    "Create marker semantics were not preserved.");
            }

            var previous = new Destination
            {
                Ignored = "existing-ignore",
                TypedIgnored = "existing-typed-ignore"
            };
            var updated = mapper.Update(source, previous, context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Automatic != 1 ||
                updated.TypedAutomatic != 2 ||
                updated.Ignored != "existing-ignore" ||
                updated.TypedIgnored != "existing-typed-ignore" ||
                updated.Explicit != "explicit!" ||
                updated.Convention != "convention")
            {
                throw new InvalidOperationException(
                    "Update marker semantics were not preserved.");
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
}
