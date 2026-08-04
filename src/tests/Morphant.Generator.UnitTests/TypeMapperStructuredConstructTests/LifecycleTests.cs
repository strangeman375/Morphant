using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperStructuredConstructTests;

[TestFixture]
internal sealed class LifecycleTests
{
    [Test]
    public void Specializes_previous_availability_without_skipping_condition_effects()
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
        public Destination(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int ConditionCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct((source, previous) =>
                {
                    if (Track() && previous.HasValue)
                    {
                        return previous;
                    }

                    return new(source.Id);
                });

        private static bool Track()
        {
            ConditionCount++;
            return true;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source { Id = 17 };
            var context = default(MappingContext);
            var created = mapper.Map(source, context);
            var createdByUpdate = mapper.Map(source, null, context);
            var previous = new Destination(31);
            var updated = mapper.Map(source, previous, context);

            if (created.Id != 17 ||
                createdByUpdate.Id != 17 ||
                !ReferenceEquals(previous, updated) ||
                TestMapper.ConditionCount != 3)
            {
                throw new InvalidOperationException(
                    "Previous specialization changed condition evaluation.");
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
    public void Keeps_an_unguarded_previous_selection_unsupported_for_Create()
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

        public bool Reuse { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct((source, previous) =>
                {
                    if (source.Reuse)
                    {
                        return previous;
                    }

                    return new(source.Id);
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var context = default(MappingContext);
            var created = mapper.Map(
                new Source { Id = 17 },
                context);
            var previous = new Destination(31);
            var updated = mapper.Map(
                new Source { Reuse = true },
                previous,
                context);

            if (created.Id != 17 ||
                !ReferenceEquals(previous, updated))
            {
                throw new InvalidOperationException(
                    "A reachable previous selection used the wrong operation semantics.");
            }

            try
            {
                mapper.Map(
                    new Source { Reuse = true },
                    context);
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Create silently removed a reachable previous selection.");
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
    public void Selects_previous_or_replacement_without_evaluating_other_branches()
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
        public Destination(int id)
        {
            Id = id;
        }

        public int Id { get; }

        public string Name { get; set; } = string.Empty;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int ConstructionCount { get; private set; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct((source, previous) =>
                {
                    if (previous.HasValue &&
                        previous.Value.Id == source.Id)
                    {
                        return previous;
                    }

                    return new(Track(source.Id));
                });

        private static int Track(int id)
        {
            ConstructionCount++;
            return id;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var context = default(MappingContext);
            var source = new Source
            {
                Id = 17,
                Name = "mapped"
            };
            var created = mapper.Map(source, context);
            var createdByUpdate = mapper.Map(source, null, context);
            var reusable = new Destination(17);
            var reused = mapper.Map(source, reusable, context);
            var replaced = mapper.Map(
                source,
                new Destination(31),
                context);

            if (created.Id != 17 ||
                created.Name != "mapped" ||
                createdByUpdate.Id != 17 ||
                createdByUpdate.Name != "mapped" ||
                !ReferenceEquals(reusable, reused) ||
                reused.Name != "mapped" ||
                replaced.Id != 17 ||
                replaced.Name != "mapped" ||
                TestMapper.ConstructionCount != 3)
            {
                throw new InvalidOperationException(
                    "Previous-aware Construct selected or evaluated the wrong branch.");
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
    public void Keeps_unsupported_constructor_branch_path_sensitive()
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
    public readonly struct Source
    {
        public int Id { get; init; }

        public bool Invalid { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int id)
        {
            Id = id;
        }

        public int Id { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source =>
                    source.Invalid
                        ? new(Ignore())
                        : new(source.Id));
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var context = default(MappingContext);
            var valid = mapper.Map(
                new Source { Id = 17 },
                context);

            if (valid.Id != 17)
            {
                throw new InvalidOperationException(
                    "The reachable constructor branch was not executed.");
            }

            try
            {
                mapper.Map(
                    new Source { Id = 17, Invalid = true },
                    context);
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "An unsupported reachable branch did not fail.");
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
