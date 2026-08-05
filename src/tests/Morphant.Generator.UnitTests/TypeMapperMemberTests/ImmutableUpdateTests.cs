using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperMemberTests;

[TestFixture]
internal sealed class ImmutableUpdateTests
{
    [Test]
    public void Allows_no_op_Update_without_explicit_construct_intent()
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

        public string Text { get; init; } = string.Empty;
    }

    public sealed class ConventionDestination
    {
        public ConventionDestination(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public sealed class SourceOnlyDestination
    {
        public SourceOnlyDestination(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public sealed class InitDestination
    {
        public int Value { get; init; }
    }

    public sealed class IgnoredDestination
    {
        public int Value { get; set; }
    }

    public sealed class ReusedDestination
    {
        public ReusedDestination(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    public sealed class ReplacementDestination
    {
        public ReplacementDestination(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int SourceOnlyConstructCount { get; private set; }

        public static int DirectConstructCount { get; private set; }

        public static int InitMemberCount { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConventionDestination>();

            builder.Map<Source, SourceOnlyDestination>()
                .Construct(source =>
                    new(value: TrackSourceOnlyConstruct(source.Value)));

            builder.Map<Source, Guid>()
                .Construct(source => TrackDirectConstruct(source.Text));

            builder.Map<Source, InitDestination>()
                .Members((source, previous) => new()
                {
                    Value = TrackInitMember(source.Value)
                });

            builder.Map<Source, IgnoredDestination>()
                .Members((source, previous) => new()
                {
                    Value = Ignore<int>()
                });

            builder.Map<Source, ReusedDestination>()
                .Construct((source, previous) =>
                {
                    if (previous.HasValue)
                    {
                        return previous;
                    }

                    return new(value: source.Value);
                });

            builder.Map<Source, ReplacementDestination>()
                .Construct((source, _) =>
                    new(value: source.Value));

            builder.Map<Source, int>();
        }

        private static int TrackSourceOnlyConstruct(int value)
        {
            SourceOnlyConstructCount++;
            return value;
        }

        private static Guid TrackDirectConstruct(string value)
        {
            DirectConstructCount++;
            return Guid.Parse(value);
        }

        private static int TrackInitMember(int value)
        {
            InitMemberCount++;
            return value;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source
            {
                Value = 9,
                Text = "00112233-4455-6677-8899-aabbccddeeff"
            };
            var context = default(MappingContext);
            var conventionMapper =
                (ITypeMapper<Source, ConventionDestination>)mapper;
            var sourceOnlyMapper =
                (ITypeMapper<Source, SourceOnlyDestination>)mapper;
            var directMapper =
                (ITypeMapper<Source, Guid>)mapper;
            var initMapper =
                (ITypeMapper<Source, InitDestination>)mapper;
            var ignoredMapper =
                (ITypeMapper<Source, IgnoredDestination>)mapper;
            var reusedMapper =
                (ITypeMapper<Source, ReusedDestination>)mapper;
            var replacementMapper =
                (ITypeMapper<Source, ReplacementDestination>)mapper;
            var scalarMapper =
                (ITypeMapper<Source, int>)mapper;

            var conventionCreated = conventionMapper.Map(source, context);
            var sourceOnlyCreated = sourceOnlyMapper.Map(source, context);
            var directCreated = directMapper.Map(source, context);
            var initCreated = initMapper.Map(source, context);

            if (conventionCreated.Value != 9 ||
                sourceOnlyCreated.Value != 9 ||
                directCreated != Guid.Parse(source.Text) ||
                initCreated.Value != 9 ||
                TestMapper.SourceOnlyConstructCount != 1 ||
                TestMapper.DirectConstructCount != 1 ||
                TestMapper.InitMemberCount != 1)
            {
                throw new InvalidOperationException(
                    "The valid Create path changed.");
            }

            var conventionPrevious = new ConventionDestination(1);
            var sourceOnlyPrevious = new SourceOnlyDestination(2);
            var directPrevious = Guid.Parse(
                "ffffffff-ffff-ffff-ffff-ffffffffffff");
            var initPrevious = new InitDestination { Value = 3 };
            var ignoredPrevious = new IgnoredDestination { Value = 4 };

            var conventionResult = conventionMapper.Map(
                source,
                conventionPrevious,
                context);
            var sourceOnlyResult = sourceOnlyMapper.Map(
                source,
                sourceOnlyPrevious,
                context);
            var directResult = directMapper.Map(
                source,
                directPrevious,
                context);
            var initResult = initMapper.Map(
                source,
                initPrevious,
                context);
            var ignoredResult = ignoredMapper.Map(
                source,
                ignoredPrevious,
                context);
            var scalarResult = scalarMapper.Map(source, 5, context);

            if (!ReferenceEquals(conventionPrevious, conventionResult) ||
                conventionResult.Value != 1 ||
                !ReferenceEquals(sourceOnlyPrevious, sourceOnlyResult) ||
                sourceOnlyResult.Value != 2 ||
                directResult != directPrevious ||
                !ReferenceEquals(initPrevious, initResult) ||
                initResult.Value != 3 ||
                !ReferenceEquals(ignoredPrevious, ignoredResult) ||
                ignoredResult.Value != 4 ||
                scalarResult != 5 ||
                TestMapper.SourceOnlyConstructCount != 1 ||
                TestMapper.DirectConstructCount != 1 ||
                TestMapper.InitMemberCount != 1)
            {
                throw new InvalidOperationException(
                    "A no-op Update did not preserve the destination.");
            }

            var previous = new ReusedDestination(2);
            var reused = reusedMapper.Map(
                source,
                previous,
                context);
            var replacementPrevious =
                new ReplacementDestination(3);
            var replacement = replacementMapper.Map(
                source,
                replacementPrevious,
                context);

            if (!ReferenceEquals(previous, reused) ||
                ReferenceEquals(replacementPrevious, replacement) ||
                replacement.Value != 9)
            {
                throw new InvalidOperationException(
                    "Explicit immutable intent was not authoritative.");
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
}
