// Compiled integration scenario: CompositionDiagnosticsTests::Rejects_every_duplicate_slot_without_executing_callbacks
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0019

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CompositionDuplicates_5c0f0019
{
    public sealed class ResultSource { }
    public sealed class ResultDestination { }

    public sealed class MembersSource
    {
        public int ReadFirst()
        {
            CallbackTracker.MembersFirst++;
            return 1;
        }

        public int ReadSecond()
        {
            CallbackTracker.MembersSecond++;
            return 2;
        }
    }

    public sealed class MembersDestination
    {
        public int Value { get; set; }
    }

    public sealed class ConvertSource { }
    public sealed class ConvertDestination { }

    public static class CallbackTracker
    {
        public static int ResultFirst;
        public static int ResultSecond;
        public static int MembersFirst;
        public static int MembersSecond;
        public static int ConvertFirst;
        public static int ConvertSecond;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ResultSource, ResultDestination>(MappingMode.Create)
                .ConstructUsing(source =>
                {
                    CallbackTracker.ResultFirst++;
                    return new ResultDestination();
                })
                .ResolveUsing((source, previous) =>
                {
                    CallbackTracker.ResultSecond++;
                    return new ResultDestination();
                });

            builder.Map<MembersSource, MembersDestination>(MappingMode.Update)
                .Members(source => new() { Value = source.ReadFirst() })
                .Members(source => new() { Value = source.ReadSecond() });

            builder.Map<ConvertSource, ConvertDestination>(MappingMode.Create)
                .Convert(source =>
                {
                    CallbackTracker.ConvertFirst++;
                    return new ConvertDestination();
                })
                .Convert((source, previous, context) =>
                {
                    CallbackTracker.ConvertSecond++;
                    return new ConvertDestination();
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            VerifyPair<ResultSource, ResultDestination>(
                mapper,
                new ResultSource(),
                new ResultDestination());
            VerifyPair<MembersSource, MembersDestination>(
                mapper,
                new MembersSource(),
                new MembersDestination());
            VerifyPair<ConvertSource, ConvertDestination>(
                mapper,
                new ConvertSource(),
                new ConvertDestination());

            if (CallbackTracker.ResultFirst != 0 ||
                CallbackTracker.ResultSecond != 0 ||
                CallbackTracker.MembersFirst != 0 ||
                CallbackTracker.MembersSecond != 0 ||
                CallbackTracker.ConvertFirst != 0 ||
                CallbackTracker.ConvertSecond != 0)
            {
                throw new InvalidOperationException(
                    "A duplicate mapping-plan callback was executed.");
            }
        }

        private static void VerifyPair<TSource, TDestination>(
            TestMapper mapper,
            TSource source,
            TDestination destination)
        {
            var contract =
                (ITypeMapper<TSource, TDestination>)mapper;

            ExpectConfigurationFailure(() => contract.Create(
                source,
                default(MappingContext)));
            ExpectConfigurationFailure(() => contract.Update(
                source,
                destination,
                default(MappingContext)));
        }

        private static void ExpectConfigurationFailure(Action action)
        {
            try
            {
                action();
            }
            catch (MappingConfigurationException exception)
            {
                if (!exception.Reason.Contains(
                        "more than once",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The duplicate-slot recovery reason was lost.");
                }

                return;
            }

            throw new InvalidOperationException(
                "A duplicate mapping plan was executed.");
        }
    }
}
