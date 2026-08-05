using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperMemberTests;

[TestFixture]
internal sealed class ImmutableUpdateTests
{
    [Test]
    public void Requires_explicit_reuse_or_replacement_for_immutable_Update()
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

    public sealed class InvalidDestination
    {
        public InvalidDestination(int value)
        {
            Value = value;
        }

        public int Value { get; }
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
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, InvalidDestination>()
                .Construct(source => new(value: source.Value));

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
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 9 };
            var context = default(MappingContext);
            var invalidMapper =
                (ITypeMapper<Source, InvalidDestination>)mapper;
            var reusedMapper =
                (ITypeMapper<Source, ReusedDestination>)mapper;
            var replacementMapper =
                (ITypeMapper<Source, ReplacementDestination>)mapper;
            var scalarMapper =
                (ITypeMapper<Source, int>)mapper;

            var created = invalidMapper.Map(source, context);

            if (created.Value != 9)
            {
                throw new InvalidOperationException(
                    "The valid Create path changed.");
            }

            ExpectUnsupported(() =>
                invalidMapper.Map(
                    source,
                    new InvalidDestination(1),
                    context));
            ExpectUnsupported(() =>
                scalarMapper.Map(source, 1, context));

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

        private static void ExpectUnsupported(Action action)
        {
            try
            {
                action();
            }
            catch (NotSupportedException exception)
                when (exception.Message ==
                    "The declarative Update would inevitably return " +
                    "the previous destination unchanged.")
            {
                return;
            }

            throw new InvalidOperationException(
                "An inevitable immutable Update no-op was accepted.");
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
