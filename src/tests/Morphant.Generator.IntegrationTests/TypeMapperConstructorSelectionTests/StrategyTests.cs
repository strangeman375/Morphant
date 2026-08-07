using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class StrategyTests
{
    [Test]
    public void Parameterless_selects_only_the_parameterless_constructor()
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
        public Destination()
        {
            Kind = "parameterless";
        }

        public Destination(int id)
        {
            Kind = "parameterized";
            Id = id;
        }

        public string Kind { get; }

        public int Id { get; }
    }

    public sealed class WithoutParameterless
    {
        public WithoutParameterless(int id) => Id = id;

        public int Id { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>()
                .ConstructorSelection(
                    ConstructorSelection.Parameterless);
            builder.Map<Source, WithoutParameterless>()
                .ConstructorSelection(
                    ConstructorSelection.Parameterless);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Id = 17 };
            var context = default(MappingContext);
            var selected =
                ((ITypeMapper<Source, Destination>)mapper)
                    .Create(source, context);
            var unsupported =
                (ITypeMapper<Source, WithoutParameterless>)mapper;
            var previous = new WithoutParameterless(31);
            var updated = unsupported.Update(
                source,
                previous,
                context);

            if (selected.Kind != "parameterless" ||
                selected.Id != 0 ||
                !ReferenceEquals(previous, updated))
            {
                throw new InvalidOperationException(
                    "Parameterless selected the wrong constructor or affected Update.");
            }

            ExpectNotSupported(() =>
                unsupported.Create(source, context));
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
                "Unavailable parameterless construction did not fail.");
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
    public void Single_counts_only_accessible_supported_constructors()
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

    public sealed class SingleDestination
    {
        public SingleDestination(int id)
        {
            Id = id;
        }

        public SingleDestination(ref int id)
        {
            Id = id;
        }

        private SingleDestination(string value)
        {
            Id = value.Length;
        }

        public int Id { get; }
    }

    public sealed class MultipleDestination
    {
        public MultipleDestination()
        {
        }

        public MultipleDestination(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, SingleDestination>()
                .ConstructorSelection(ConstructorSelection.Single);
            builder.Map<Source, MultipleDestination>()
                .ConstructorSelection(ConstructorSelection.Single);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Id = 17 };
            var context = default(MappingContext);
            var single =
                ((ITypeMapper<Source, SingleDestination>)mapper)
                    .Create(source, context);

            if (single.Id != 17)
            {
                throw new InvalidOperationException(
                    "Single did not select the only supported constructor.");
            }

            ExpectNotSupported(() =>
                ((ITypeMapper<Source, MultipleDestination>)mapper)
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
                "Multiple supported constructors were treated as Single.");
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
    public void Unambiguous_prefers_one_parameterized_constructor_without_fallback()
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

    public sealed class PreferredDestination
    {
        public PreferredDestination()
        {
            Kind = "parameterless";
        }

        public PreferredDestination(int id)
        {
            Kind = "parameterized";
            Id = id;
        }

        public string Kind { get; }

        public int Id { get; }
    }

    public sealed class ParameterlessOnly
    {
        public string Kind { get; } = "parameterless";
    }

    public sealed class AmbiguousDestination
    {
        public AmbiguousDestination()
        {
        }

        public AmbiguousDestination(int id)
        {
        }

        public AmbiguousDestination(string value)
        {
        }
    }

    public sealed class NoFallbackDestination
    {
        public NoFallbackDestination()
        {
            Kind = "parameterless";
        }

        public NoFallbackDestination(string missing)
        {
            Kind = missing;
        }

        public string Kind { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, PreferredDestination>()
                .ConstructorSelection(
                    ConstructorSelection.Unambiguous);
            builder.Map<Source, ParameterlessOnly>()
                .ConstructorSelection(
                    ConstructorSelection.Unambiguous);
            builder.Map<Source, AmbiguousDestination>()
                .ConstructorSelection(
                    ConstructorSelection.Unambiguous);
            builder.Map<Source, NoFallbackDestination>()
                .ConstructorSelection(
                    ConstructorSelection.Unambiguous);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Id = 17 };
            var context = default(MappingContext);
            var preferred =
                ((ITypeMapper<Source, PreferredDestination>)mapper)
                    .Create(source, context);
            var parameterless =
                ((ITypeMapper<Source, ParameterlessOnly>)mapper)
                    .Create(source, context);

            if (preferred.Kind != "parameterized" ||
                preferred.Id != 17 ||
                parameterless.Kind != "parameterless")
            {
                throw new InvalidOperationException(
                    "Unambiguous selected the wrong constructor.");
            }

            ExpectNotSupported(() =>
                ((ITypeMapper<Source, AmbiguousDestination>)mapper)
                    .Create(source, context));
            ExpectNotSupported(() =>
                ((ITypeMapper<Source, NoFallbackDestination>)mapper)
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
                "Unambiguous construction unexpectedly succeeded.");
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
    public void Explicit_disables_automatic_construction_only()
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
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructorSelection(ConstructorSelection.Explicit);
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source { Value = 17 };
            var previous = new Destination { Value = 31 };
            var context = default(MappingContext);
            var updated = mapper.Update(source, previous, context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Value != 17)
            {
                throw new InvalidOperationException(
                    "Explicit affected mapping of an existing destination.");
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
                "Explicit allowed automatic construction.");
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
