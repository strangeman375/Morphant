// Compiled integration scenario: TypeMapperInheritanceTests/ExactSamePairTests::Inherits_the_full_exact_pair_plan_and_applies_local_precedence
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExactSamePair_aa11ce0f
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class ConstructDestination
    {
        public int Value { get; set; }
    }

    public sealed class ResolveDestination
    {
        public int Value { get; set; }
    }

    public sealed class ConstructUsingDestination
    {
        public int Value { get; set; }
    }

    public sealed class ResolveUsingDestination
    {
        public int Value { get; set; }
    }

    public sealed class MembersDestination
    {
        public int Value { get; set; }
    }

    public sealed class ConvertDestination
    {
        public int Value { get; set; }
    }

    public sealed class DeclarativeOverridesConvertDestination
    {
        public int Value { get; set; }
    }

    public sealed class ResultOverrideDestination
    {
        public ResultOverrideDestination(int origin) => Origin = origin;

        public int Origin { get; }

        public int Value { get; set; }
    }

    public sealed class ConvertOverridesDeclarativeDestination
    {
        public ConvertOverridesDeclarativeDestination(int origin) =>
            Origin = origin;

        public int Origin { get; }

        public int Value { get; set; }
    }

    public abstract class BaseMapper : TypeMapper<BaseMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConstructDestination>()
                .Construct(source => new())
                .Members(source => new() { Value = source.Value + 1 });

            builder.Map<Source, ResolveDestination>()
                .Resolve((source, previous) => new())
                .Members(source => new() { Value = source.Value + 2 });

            builder.Map<Source, ConstructUsingDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .ConstructUsing(source => new ConstructUsingDestination
                {
                    Value = source.Value + 3
                });

            builder.Map<Source, ResolveUsingDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .ResolveUsing((source, previous) =>
                    new ResolveUsingDestination
                    {
                        Value = source.Value + 4
                    });

            builder.Map<Source, MembersDestination>()
                .Members(source => new() { Value = source.Value + 5 });

            builder.Map<Source, ConvertDestination>()
                .Convert(source => new ConvertDestination
                {
                    Value = source!.Value + 6
                });

            builder.Map<Source, DeclarativeOverridesConvertDestination>()
                .Convert(source =>
                    new DeclarativeOverridesConvertDestination
                    {
                        Value = source!.Value + 7
                    });

            builder.Map<Source, ResultOverrideDestination>()
                .Construct(source => new(8))
                .Members(source => new() { Value = source.Value + 8 });

            builder.Map<Source, ConvertOverridesDeclarativeDestination>()
                .Construct(source => new(10))
                .Members(source => new() { Value = source.Value + 10 });
        }
    }

    [MorphantMapper]
    public partial class TestMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);

            builder.Map<Source, ConstructDestination>()
                .IncludeBase<Source, ConstructDestination>();
            builder.Map<Source, ResolveDestination>()
                .IncludeBase<Source, ResolveDestination>();
            builder.Map<Source, ConstructUsingDestination>()
                .IncludeBase<Source, ConstructUsingDestination>();
            builder.Map<Source, ResolveUsingDestination>()
                .IncludeBase<Source, ResolveUsingDestination>();
            builder.Map<Source, MembersDestination>()
                .IncludeBase<Source, MembersDestination>();
            builder.Map<Source, ConvertDestination>()
                .IncludeBase<Source, ConvertDestination>();

            builder.Map<Source, DeclarativeOverridesConvertDestination>()
                .IncludeBase<Source,
                    DeclarativeOverridesConvertDestination>()
                .Members(source => new()
                {
                    Value = source.Value + 70
                });

            builder.Map<Source, ResultOverrideDestination>()
                .IncludeBase<Source, ResultOverrideDestination>()
                .Construct(source => new(9));

            builder.Map<Source, ConvertOverridesDeclarativeDestination>()
                .IncludeBase<Source,
                    ConvertOverridesDeclarativeDestination>()
                .Convert(source =>
                    new ConvertOverridesDeclarativeDestination(11)
                    {
                        Value = source!.Value + 11
                    });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 20 };

            AssertValue<ConstructDestination>(
                mapper,
                source,
                21,
                static destination => destination.Value);
            AssertValue<ResolveDestination>(
                mapper,
                source,
                22,
                static destination => destination.Value);
            AssertValue<ConstructUsingDestination>(
                mapper,
                source,
                23,
                static destination => destination.Value);
            AssertValue<ResolveUsingDestination>(
                mapper,
                source,
                24,
                static destination => destination.Value);
            AssertValue<MembersDestination>(
                mapper,
                source,
                25,
                static destination => destination.Value);
            AssertValue<ConvertDestination>(
                mapper,
                source,
                26,
                static destination => destination.Value);
            AssertValue<DeclarativeOverridesConvertDestination>(
                mapper,
                source,
                90,
                static destination => destination.Value);

            var resultOverride = Create<ResultOverrideDestination>(
                mapper,
                source);

            if (resultOverride.Origin != 9 || resultOverride.Value != 28)
            {
                throw new InvalidOperationException(
                    "The local exact-pair result did not replace the " +
                    "inherited result while retaining inherited members.");
            }

            var convertOverride =
                Create<ConvertOverridesDeclarativeDestination>(
                    mapper,
                    source);

            if (convertOverride.Origin != 11 ||
                convertOverride.Value != 31)
            {
                throw new InvalidOperationException(
                    "The local Convert did not replace the inherited " +
                    "declarative plan.");
            }
        }

        private static void AssertValue<TDestination>(
            TestMapper mapper,
            Source source,
            int expected,
            Func<TDestination, int> readValue)
        {
            var destination = Create<TDestination>(mapper, source);
            var value = readValue(destination);

            if (value != expected)
            {
                throw new InvalidOperationException(
                    $"Expected {expected} for {typeof(TDestination)}, " +
                    $"observed {value}.");
            }
        }

        private static TDestination Create<TDestination>(
            TestMapper mapper,
            Source source)
        {
            return ((ITypeMapper<Source, TDestination>)mapper)
                .Create(source, default(MappingContext));
        }
    }
}
