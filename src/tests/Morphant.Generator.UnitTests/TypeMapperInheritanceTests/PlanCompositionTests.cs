using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class PlanCompositionTests
{
    [Test]
    public void Merges_Members_by_destination_member_and_rebuilds_dependencies()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public string Name { get; init; } = string.Empty;

        public string Code { get; init; } = string.Empty;

        public string Kept { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string Kept { get; set; } = string.Empty;

        public string Extra { get; set; } = string.Empty;
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected static string ObsoleteName(Source source) =>
            throw new InvalidOperationException(
                "An overridden dependency was evaluated.");

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .NullSourceHandling(NullSourceHandling.Throw)
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, _) => new()
                {
                    Name = ObsoleteName(source),
                    Code = "base:" + source.Code,
                    Kept = "base:" + source.Kept
                });
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>()
                .IncludeBase()
                .Members((source, _, result) => new()
                {
                    Name = "derived:" + source.Name,
                    Code = Ignore(),
                    Extra = result.Name + ":extra"
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new DerivedMapper();
            var result = mapper.Create(
                new Source
                {
                    Name = "name",
                    Code = "code",
                    Kept = "kept"
                },
                default);

            if (result.Name != "derived:name" ||
                result.Code != string.Empty ||
                result.Kept != "base:kept" ||
                result.Extra != "derived:name:extra")
            {
                throw new InvalidOperationException(
                    "The effective Members plan was composed incorrectly.");
            }

            try
            {
                mapper.Create(null, default);
            }
            catch (ArgumentNullException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Included pair settings were not inherited.");
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Keeps_convention_only_creation_members_out_of_the_explicit_factory_check()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public int Seed { get; init; }

        public string BaseValue { get; init; } = string.Empty;

        public string DerivedValue { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public Destination(int seed) => Seed = seed;

        public int Seed { get; }

        public string BaseValue { get; set; } = string.Empty;

        public string DerivedValue { get; set; } = string.Empty;
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(ByFactory(() =>
                    new Destination(source.Seed))))
                .Members((source, _) => new()
                {
                    BaseValue = "base:" + source.BaseValue
                });
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>()
                .IncludeBase()
                .Members((source, _) => new()
                {
                    DerivedValue = "derived:" + source.DerivedValue
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Source, Destination>)new DerivedMapper())
                    .Create(
                        new Source
                        {
                            Seed = 17,
                            BaseValue = "base",
                            DerivedValue = "derived"
                        },
                        default);

            if (result.Seed != 17 ||
                result.BaseValue != "base:base" ||
                result.DerivedValue != "derived:derived")
            {
                throw new InvalidOperationException(
                    "Members composition changed ByFactory applicability.");
            }
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Uses_the_nearest_base_pair_and_replaces_Construct_as_a_unit()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class NearestDestination
    {
        public NearestDestination(string kind) => Kind = kind;

        public string Kind { get; }
    }

    public sealed class ReplacementDestination
    {
        public ReplacementDestination(string kind) => Kind = kind;

        public string Kind { get; }
    }

    public abstract class FarMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, NearestDestination>()
                .Construct(source => new("far:" + source.Value));
            builder.Map<Source, ReplacementDestination>()
                .Construct(source => new("far:" + source.Value));
        }
    }

    public abstract class NearMapper : FarMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, NearestDestination>()
                .IncludeBase()
                .Construct(source => new("near:" + source.Value));
            builder.Map<Source, ReplacementDestination>()
                .IncludeBase()
                .Construct(source => new("near:" + source.Value));
        }
    }

    [MorphantMapper]
    public partial class DerivedMapper : NearMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, NearestDestination>()
                .IncludeBase();
            builder.Map<Source, ReplacementDestination>()
                .IncludeBase()
                .Construct(source => new("current:" + source.Value));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new DerivedMapper();
            var source = new Source { Value = 17 };
            var nearest =
                ((ITypeMapper<Source, NearestDestination>)mapper)
                    .Create(source, default);
            var replacement =
                ((ITypeMapper<Source, ReplacementDestination>)mapper)
                    .Create(source, default);

            if (nearest.Kind != "near:17" ||
                replacement.Kind != "current:17")
            {
                throw new InvalidOperationException(
                    "Construct inheritance did not use replacement semantics.");
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
    public void Replaces_Convert_and_rejects_partial_manual_declarative_mix()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class DeclarativeDestination
    {
        public DeclarativeDestination(string kind) => Kind = kind;

        public string Kind { get; }
    }

    public sealed class ManualDestination
    {
        public ManualDestination(string kind) => Kind = kind;

        public string Kind { get; }
    }

    public sealed class MixedDestination
    {
        public string Kind { get; set; } = string.Empty;
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, DeclarativeDestination>()
                .Construct(source => new("base:" + source.Value));
            builder.Map<Source, ManualDestination>()
                .Convert((source, _, _) =>
                    new ManualDestination("base:" + source!.Value));
            builder.Map<Source, MixedDestination>()
                .Convert((source, _, _) => new MixedDestination
                {
                    Kind = "base:" + source!.Value
                });
        }
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, DeclarativeDestination>()
                .IncludeBase()
                .Convert((source, _, _) =>
                    new DeclarativeDestination(
                        "current:" + source!.Value));
            builder.Map<Source, ManualDestination>()
                .IncludeBase()
                .Convert((source, _, _) =>
                    new ManualDestination("current:" + source!.Value));
            builder.Map<Source, MixedDestination>()
                .IncludeBase()
                .Members((source, _) => new()
                {
                    Kind = "current:" + source.Value
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new DerivedMapper();
            var source = new Source { Value = 17 };
            var declarative =
                ((ITypeMapper<Source, DeclarativeDestination>)mapper)
                    .Create(source, default);
            var manual =
                ((ITypeMapper<Source, ManualDestination>)mapper)
                    .Create(source, default);

            if (declarative.Kind != "current:17" ||
                manual.Kind != "current:17")
            {
                throw new InvalidOperationException(
                    "A local Convert did not replace the inherited plan.");
            }

            try
            {
                ((ITypeMapper<Source, MixedDestination>)mapper)
                    .Create(source, default);
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "An inherited manual plan was partially mixed with Members.");
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
