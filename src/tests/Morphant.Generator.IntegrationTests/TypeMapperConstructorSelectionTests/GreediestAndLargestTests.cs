using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class GreediestAndLargestTests
{
    [Test]
    public void Greediest_selects_the_unique_plan_with_most_emitted_arguments()
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
    public sealed class SparseSource
    {
        public int Id { get; init; }

        public int Code { get; init; }
    }

    public sealed class RichSource
    {
        public int Id { get; init; }

        public int Code { get; init; }

        public string Label { get; init; } = string.Empty;

        public string[] Tags { get; init; } = Array.Empty<string>();
    }

    public sealed class ApplicableDestination
    {
        public ApplicableDestination(int id)
        {
            Kind = "applicable";
            Value = id;
        }

        public ApplicableDestination(int code, string missing)
        {
            Kind = missing;
            Value = code;
        }

        public string Kind { get; }

        public int Value { get; }
    }

    public sealed class RichDestination
    {
        public RichDestination(int id)
        {
            Kind = "small";
            Value = id;
            Label = string.Empty;
            Tags = Array.Empty<string>();
        }

        public RichDestination(
            int code,
            string label = "default",
            params string[] tags)
        {
            Kind = "rich";
            Value = code;
            Label = label;
            Tags = tags;
        }

        public string Kind { get; }

        public int Value { get; }

        public string Label { get; }

        public string[] Tags { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<SparseSource, ApplicableDestination>()
                .ConstructorSelection(ConstructorSelection.Greediest);
            builder.Map<RichSource, RichDestination>()
                .ConstructorSelection(ConstructorSelection.Greediest);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var applicable =
                ((ITypeMapper<SparseSource, ApplicableDestination>)mapper)
                    .Create(
                        new SparseSource { Id = 17, Code = 31 },
                        context);
            var tags = new[] { "one", "two" };
            var rich =
                ((ITypeMapper<RichSource, RichDestination>)mapper)
                    .Create(
                        new RichSource
                        {
                            Id = 17,
                            Code = 31,
                            Label = "mapped",
                            Tags = tags
                        },
                        context);

            if (applicable.Kind != "applicable" ||
                applicable.Value != 17 ||
                rich.Kind != "rich" ||
                rich.Value != 31 ||
                rich.Label != "mapped" ||
                !ReferenceEquals(tags, rich.Tags))
            {
                throw new InvalidOperationException(
                    "Greediest did not maximize emitted arguments.");
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
    public void Greediest_requires_an_explicit_choice_when_best_scores_tie()
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

        public int Code { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int id)
        {
        }

        public Destination(
            int code,
            string label = "default",
            params string[] tags)
        {
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructorSelection(ConstructorSelection.Greediest);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();

            try
            {
                mapper.Create(
                    new Source { Id = 17, Code = 31 },
                    default(MappingContext));
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Omitted optional and params arguments changed the Greediest score.");
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
    public void Greediest_excludes_nullable_warning_and_required_member_failures()
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
    public partial class TestMapper : TypeMapper
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
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp11,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Largest_uses_declared_size_and_never_falls_back()
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

        public int Code { get; init; }
    }

    public sealed class LargestDestination
    {
        public LargestDestination(int id)
        {
            Kind = "small";
            Value = id;
        }

        public LargestDestination(
            int code,
            string label = "default",
            params string[] tags)
        {
            Kind = "largest:" + label + ":" + tags.Length;
            Value = code;
        }

        public string Kind { get; }

        public int Value { get; }
    }

    public sealed class NoFallbackDestination
    {
        public NoFallbackDestination(int id)
        {
        }

        public NoFallbackDestination(int code, string missing)
        {
        }
    }

    public sealed class TiedDestination
    {
        public TiedDestination(int id)
        {
        }

        public TiedDestination(string missing)
        {
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, LargestDestination>()
                .ConstructorSelection(ConstructorSelection.Largest);
            builder.Map<Source, NoFallbackDestination>()
                .ConstructorSelection(ConstructorSelection.Largest);
            builder.Map<Source, TiedDestination>()
                .ConstructorSelection(ConstructorSelection.Largest);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Id = 17, Code = 31 };
            var context = default(MappingContext);
            var largest =
                ((ITypeMapper<Source, LargestDestination>)mapper)
                    .Create(source, context);

            if (largest.Kind != "largest:default:0" ||
                largest.Value != 31)
            {
                throw new InvalidOperationException(
                    "Largest did not select by declared parameter count.");
            }

            ExpectNotSupported(() =>
                ((ITypeMapper<Source, NoFallbackDestination>)mapper)
                    .Create(source, context));
            ExpectNotSupported(() =>
                ((ITypeMapper<Source, TiedDestination>)mapper)
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
                "Largest unexpectedly fell back or resolved a tie.");
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
