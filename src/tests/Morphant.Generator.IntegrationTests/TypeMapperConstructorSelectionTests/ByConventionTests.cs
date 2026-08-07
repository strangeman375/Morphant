using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperConstructorSelectionTests;

[TestFixture]
internal sealed class ByConventionTests
{
    [Test]
    public void Applies_selection_to_ByConvention_but_not_explicit_Construct()
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

    public sealed class ParameterlessDestination
    {
        public ParameterlessDestination()
        {
            Kind = "parameterless";
        }

        public ParameterlessDestination(int id)
        {
            Kind = id.ToString();
        }

        public string Kind { get; }
    }

    public sealed class ExplicitByConventionDestination
    {
        public ExplicitByConventionDestination()
        {
        }
    }

    public sealed class ExplicitConstructDestination
    {
        public ExplicitConstructDestination()
        {
            Kind = "parameterless";
        }

        public ExplicitConstructDestination(int id)
        {
            Kind = id.ToString();
        }

        public string Kind { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ParameterlessDestination>()
                .ConstructorSelection(
                    ConstructorSelection.Parameterless)
                .Construct(_ => new(ByConvention()));
            builder.Map<Source, ExplicitByConventionDestination>()
                .ConstructorSelection(ConstructorSelection.Explicit)
                .Construct(_ => new(ByConvention()));
            builder.Map<Source, ExplicitConstructDestination>()
                .ConstructorSelection(ConstructorSelection.Explicit)
                .Construct(source => new(source.Id));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Id = 17 };
            var context = default(MappingContext);
            var parameterless =
                ((ITypeMapper<Source, ParameterlessDestination>)mapper)
                    .Create(source, context);
            var explicitConstruct =
                ((ITypeMapper<Source, ExplicitConstructDestination>)mapper)
                    .Create(source, context);

            if (parameterless.Kind != "parameterless" ||
                explicitConstruct.Kind != "17")
            {
                throw new InvalidOperationException(
                    "ConstructorSelection crossed its applicability boundary.");
            }

            try
            {
                ((ITypeMapper<Source, ExplicitByConventionDestination>)mapper)
                    .Create(source, context);
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Explicit allowed ByConvention construction.");
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
    public void Greediest_counts_written_ByConvention_rules_and_omissions()
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

        public string Name { get; init; } = string.Empty;

        public string Label { get; init; } = string.Empty;
    }

    public sealed class RuleDestination
    {
        public RuleDestination(int id)
        {
            Kind = "id";
            Value = id;
        }

        public RuleDestination(string name, int code = 0)
        {
            Kind = name;
            Value = code;
        }

        public string Kind { get; }

        public int Value { get; }
    }

    public sealed class IgnoredDestination
    {
        public IgnoredDestination(
            int id,
            string label = "default")
        {
        }

        public IgnoredDestination(
            int code,
            string label = "default",
            params string[] tags)
        {
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, RuleDestination>()
                .ConstructorSelection(ConstructorSelection.Greediest)
                .Construct(source => new(
                    ByConvention(),
                    new()
                    {
                        name = source.Name,
                        code = 47
                    }));
            builder.Map<Source, IgnoredDestination>()
                .ConstructorSelection(ConstructorSelection.Greediest)
                .Construct(_ => new(
                    ByConvention(),
                    new()
                    {
                        label = Ignore()
                    }));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source
            {
                Id = 17,
                Code = 31,
                Name = "configured",
                Label = "automatic"
            };
            var context = default(MappingContext);
            var selected =
                ((ITypeMapper<Source, RuleDestination>)mapper)
                    .Create(source, context);

            if (selected.Kind != "configured" ||
                selected.Value != 47)
            {
                throw new InvalidOperationException(
                    "Written ByConvention rules did not participate in Greediest.");
            }

            try
            {
                ((ITypeMapper<Source, IgnoredDestination>)mapper)
                    .Create(source, context);
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Ignored ByConvention arguments changed the Greediest score.");
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
    public void Rejects_warning_producing_automatic_ByConvention_arguments()
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
        public string? Name { get; init; }
    }

    public sealed class Destination
    {
        public Destination(string name, int code = 0)
        {
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructorSelection(ConstructorSelection.Single)
                .Construct(_ => new(
                    ByConvention(),
                    new()
                    {
                        code = 47
                    }));
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
                    new Source { Name = null },
                    default(MappingContext));
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A nullable warning was accepted for automatic ByConvention mapping.");
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
