// Compiled integration scenario: TypeMapperConvertTests/InvalidPlanTests::Rejects_captures_duplicates_mixed_plans_and_map_settings
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0019, MORPH0020

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InvalidPlan_521c7ef8
{
    public sealed record Source(int Value);

    public sealed record CapturedDestination(int Value);

    public sealed record FunctionDestination(int Value);

    public sealed record DuplicateDestination(int Value);

    public sealed record MixedConstructDestination(int Value);

    public sealed class MixedMembersDestination
    {
        public int Value { get; set; }
    }

    public sealed record SettingDestination(int Value);

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var offset = Environment.TickCount;

            CapturedDestination Create(Source? source) =>
                new((source?.Value ?? 0) + 1);

            builder.Map<Source, CapturedDestination>()
                .Convert((source, _, _) =>
                    new((source?.Value ?? 0) + offset));

            builder.Map<Source, FunctionDestination>()
                .Convert((source, _, _) =>
                    new FunctionDestination(Create(source).Value));

            builder.Map<Source, DuplicateDestination>()
                .Convert((source, _, _) =>
                    new(source?.Value ?? 0))
                .Convert((source, _, _) =>
                    new((source?.Value ?? 0) + 1));

            builder.Map<Source, MixedConstructDestination>()
                .Construct(source => new(source.Value))
                .Convert((source, _, _) =>
                    new(source?.Value ?? 0));

            builder.Map<Source, MixedMembersDestination>()
                .Members((source, _) => new()
                {
                    Value = source.Value
                })
                .Convert((source, _, _) => new()
                {
                    Value = source?.Value ?? 0
                });

            builder.Map<Source, SettingDestination>()
                .NullSourceHandling(NullSourceHandling.ReturnNull)
                .Convert((source, _, _) =>
                    new(source?.Value ?? 0));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var source = new Source(3);

            ExpectUnsupported(() =>
                ((ITypeMapper<Source, CapturedDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, FunctionDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, DuplicateDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, MixedConstructDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, MixedMembersDestination>)mapper)
                .Create(source, context));
            ExpectUnsupported(() =>
                ((ITypeMapper<Source, SettingDestination>)mapper)
                .Create(source, context));
        }

        private static void ExpectUnsupported(Action action)
        {
            try
            {
                action();
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "An invalid Convert plan was executed.");
        }
    }
}
