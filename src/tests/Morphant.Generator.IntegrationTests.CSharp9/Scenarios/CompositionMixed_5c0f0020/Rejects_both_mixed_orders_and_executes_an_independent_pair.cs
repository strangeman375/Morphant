// Compiled integration scenario: CompositionDiagnosticsTests::Rejects_both_mixed_orders_and_executes_an_independent_pair
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0020

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CompositionMixed_5c0f0020
{
    public sealed class ResultFirstSource { }
    public sealed class ResultFirstDestination { }

    public sealed class ConvertFirstSource
    {
        public int ReadValue()
        {
            CallbackTracker.Members++;
            return 7;
        }
    }

    public sealed class ConvertFirstDestination
    {
        public int Value { get; set; }
    }

    public sealed class IndependentSource
    {
        public int Value { get; set; }
    }

    public sealed class IndependentDestination
    {
        public int Value { get; set; }
    }

    public static class CallbackTracker
    {
        public static int Result;
        public static int ConvertAfterResult;
        public static int ConvertBeforeMembers;
        public static int Members;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ResultFirstSource, ResultFirstDestination>()
                .ConstructUsing(source =>
                {
                    CallbackTracker.Result++;
                    return new ResultFirstDestination();
                })
                .Convert(source =>
                {
                    CallbackTracker.ConvertAfterResult++;
                    return new ResultFirstDestination();
                });

            builder.Map<ConvertFirstSource, ConvertFirstDestination>()
                .Convert(source =>
                {
                    CallbackTracker.ConvertBeforeMembers++;
                    return new ConvertFirstDestination();
                })
                .Members(source => new() { Value = source.ReadValue() });

            builder.Map<IndependentSource, IndependentDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            VerifyBrokenPair<ResultFirstSource, ResultFirstDestination>(
                mapper,
                new ResultFirstSource(),
                new ResultFirstDestination());
            VerifyBrokenPair<ConvertFirstSource, ConvertFirstDestination>(
                mapper,
                new ConvertFirstSource(),
                new ConvertFirstDestination());

            if (CallbackTracker.Result != 0 ||
                CallbackTracker.ConvertAfterResult != 0 ||
                CallbackTracker.ConvertBeforeMembers != 0 ||
                CallbackTracker.Members != 0)
            {
                throw new InvalidOperationException(
                    "A mixed mapping-plan callback was executed.");
            }

            var independent =
                (ITypeMapper<IndependentSource, IndependentDestination>)mapper;
            var created = independent.Create(
                new IndependentSource { Value = 31 },
                default(MappingContext));
            var updated = independent.Update(
                new IndependentSource { Value = 37 },
                created,
                default(MappingContext));

            if (!ReferenceEquals(created, updated) || updated.Value != 37)
            {
                throw new InvalidOperationException(
                    "The independent pair did not remain executable.");
            }
        }

        private static void VerifyBrokenPair<TSource, TDestination>(
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
                        "Convert is combined",
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "The mixed-plan recovery reason was lost.");
                }

                return;
            }

            throw new InvalidOperationException(
                "A mixed mapping plan was executed.");
        }
    }
}
