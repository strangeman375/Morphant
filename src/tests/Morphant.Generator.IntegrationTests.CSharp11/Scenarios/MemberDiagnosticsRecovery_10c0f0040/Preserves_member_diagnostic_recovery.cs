// Compiled integration scenario: MemberDiagnosticsTests::Preserves_suppressed_member_diagnostic_recovery
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0040
#pragma warning disable MORPH0041
#pragma warning disable MORPH0042
#pragma warning disable MORPH0043

using System;
using System.Diagnostics.CodeAnalysis;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.MemberDiagnosticsRecovery_10c0f0040
{
    public sealed class Source
    {
        public bool UseInvalid { get; init; }

        public bool ReturnNull { get; init; }

        public int Value { get; init; }
    }

    public sealed class InvalidRuleDestination
    {
        public int Value { get; set; }

        public int Missing { get; set; }
    }

    public sealed class RequiredDestination
    {
        public static int ConstructorCalls { get; private set; }

        public RequiredDestination() => ConstructorCalls++;

        public required int Value { get; init; }

        public int Other { get; set; }

        public static void Reset() => ConstructorCalls = 0;
    }

    public sealed class SatisfiedRequiredDestination
    {
        [SetsRequiredMembers]
        public SatisfiedRequiredDestination() => Value = 37;

        public required int Value { get; init; }
    }

    public sealed class LifecycleDestination
    {
        public static int ConstructorCalls { get; private set; }

        public LifecycleDestination() => ConstructorCalls++;

        public int Value { get; init; }

        public static void Reset() => ConstructorCalls = 0;
    }

    public sealed class RuntimeLifecycleDestination
    {
        public int Value { get; init; }
    }

    public sealed class ConditionalLifecycleDestination
    {
        public static int ConstructorCalls { get; private set; }

        public ConditionalLifecycleDestination() => ConstructorCalls++;

        public int Initial { get; init; }

        public int Value { get; set; }

        public static void Reset() => ConstructorCalls = 0;
    }

    public sealed class NullPlanDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static int InvalidRuleReads { get; private set; }

        public static int RequiredRuleReads { get; private set; }

        public static int LifecycleRuleReads { get; private set; }

        public static int RuntimeFactoryCalls { get; private set; }

        public static int RuntimeRuleReads { get; private set; }

        public static int ConditionalLifecycleRuleReads { get; private set; }

        public static int NullPlanRuleReads { get; private set; }

        public static void Reset()
        {
            InvalidRuleReads = 0;
            RequiredRuleReads = 0;
            LifecycleRuleReads = 0;
            RuntimeFactoryCalls = 0;
            RuntimeRuleReads = 0;
            ConditionalLifecycleRuleReads = 0;
            NullPlanRuleReads = 0;
            RequiredDestination.Reset();
            LifecycleDestination.Reset();
            ConditionalLifecycleDestination.Reset();
        }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, InvalidRuleDestination>()
                .Members(source => source.UseInvalid
                    ? new()
                    {
                        Value = ReadInvalidRuleValue(),
                        Missing = Auto()
                    }
                    : new()
                    {
                        Value = source.Value,
                        Missing = Ignore()
                    });

            builder.Map<Source, RequiredDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members(source => new()
                {
                    Other = ReadRequiredRuleValue()
                });

            builder.Map<Source, SatisfiedRequiredDestination>()
                .MemberSelection(MemberSelection.Explicit);

            builder.Map<Source, LifecycleDestination>()
                .Members((source, previous, result) => new()
                {
                    Value = ReadLifecycleRuleValue(result)
                });

            builder.Map<Source, RuntimeLifecycleDestination>()
                .ConstructUsing(BuildRuntimeResult)
                .Members(source => new()
                {
                    Value = ReadRuntimeRuleValue(source.Value)
                });

            builder.Map<Source, ConditionalLifecycleDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Members((source, previous, result) => result.Value >= 0
                    ? new()
                    {
                        Initial = ReadConditionalLifecycleRuleValue()
                    }
                    : new()
                    {
                        Value = source.Value
                    });

            builder.Map<Source, NullPlanDestination>()
                .Members(source => source.UseInvalid
                    ? default!
                    : new()
                    {
                        Value = ReadNullPlanRuleValue(source.Value)
                    });
        }

        private static int ReadInvalidRuleValue()
        {
            InvalidRuleReads++;
            return 101;
        }

        private static int ReadRequiredRuleValue()
        {
            RequiredRuleReads++;
            return 103;
        }

        private static int ReadLifecycleRuleValue(
            LifecycleDestination result)
        {
            LifecycleRuleReads++;
            return result.Value + 1;
        }

        private static RuntimeLifecycleDestination BuildRuntimeResult(
            Source source)
        {
            RuntimeFactoryCalls++;
            return source.ReturnNull
                ? null!
                : new RuntimeLifecycleDestination();
        }

        private static int ReadRuntimeRuleValue(int value)
        {
            RuntimeRuleReads++;
            return value;
        }

        private static int ReadConditionalLifecycleRuleValue()
        {
            ConditionalLifecycleRuleReads++;
            return 107;
        }

        private static int ReadNullPlanRuleValue(int value)
        {
            NullPlanRuleReads++;
            return value;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            TestMapper.Reset();

            VerifyInvalidRule(mapper, context);
            VerifyRequiredMember(mapper, context);
            VerifyLifecycle(mapper, context);
            VerifyRuntimeLifecycle(mapper, context);
            VerifyConditionalLifecycle(mapper, context);
            VerifyNullPlan(mapper, context);
        }

        private static void VerifyInvalidRule(
            TestMapper mapper,
            MappingContext context)
        {
            var typed = (ITypeMapper<Source, InvalidRuleDestination>)mapper;
            var valid = typed.Create(
                new Source { Value = 17 },
                context);

            if (valid.Value != 17 || TestMapper.InvalidRuleReads != 0)
            {
                throw new InvalidOperationException(
                    "A valid MORPH0040 sibling branch was changed.");
            }

            var previous = new InvalidRuleDestination
            {
                Value = 23,
                Missing = 29
            };

            ExpectConfiguration(() => typed.Update(
                new Source { UseInvalid = true },
                previous,
                context));

            if (previous.Value != 23 ||
                previous.Missing != 29 ||
                TestMapper.InvalidRuleReads != 0)
            {
                throw new InvalidOperationException(
                    "MORPH0040 recovery partially evaluated or mutated " +
                    "the invalid member leaf.");
            }
        }

        private static void VerifyRequiredMember(
            TestMapper mapper,
            MappingContext context)
        {
            var typed = (ITypeMapper<Source, RequiredDestination>)mapper;

            ExpectConfiguration(() => typed.Create(new Source(), context));
            ExpectConfiguration(() => typed.Update(
                new Source(),
                null,
                context));

            if (RequiredDestination.ConstructorCalls != 0 ||
                TestMapper.RequiredRuleReads != 0)
            {
                throw new InvalidOperationException(
                    "MORPH0041 recovery ran construction or member values.");
            }

            var previous = new RequiredDestination { Value = 31 };
            RequiredDestination.Reset();
            var updated = typed.Update(new Source(), previous, context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Value != 31 ||
                updated.Other != 103 ||
                RequiredDestination.ConstructorCalls != 0 ||
                TestMapper.RequiredRuleReads != 1)
            {
                throw new InvalidOperationException(
                    "MORPH0041 recovery changed the existing destination " +
                    "path.");
            }

            var satisfied =
                ((ITypeMapper<Source, SatisfiedRequiredDestination>)mapper)
                .Create(new Source(), context);

            if (satisfied.Value != 37)
            {
                throw new InvalidOperationException(
                    "SetsRequiredMembers stopped satisfying the obligation.");
            }
        }

        private static void VerifyLifecycle(
            TestMapper mapper,
            MappingContext context)
        {
            var typed = (ITypeMapper<Source, LifecycleDestination>)mapper;

            ExpectConfiguration(() => typed.Create(new Source(), context));

            if (LifecycleDestination.ConstructorCalls != 0 ||
                TestMapper.LifecycleRuleReads != 0)
            {
                throw new InvalidOperationException(
                    "Structured MORPH0042 recovery ran before-result work.");
            }

            var previous = new LifecycleDestination { Value = 41 };
            LifecycleDestination.Reset();
            var updated = typed.Update(new Source(), previous, context);

            if (!ReferenceEquals(previous, updated) ||
                updated.Value != 41 ||
                LifecycleDestination.ConstructorCalls != 0 ||
                TestMapper.LifecycleRuleReads != 0)
            {
                throw new InvalidOperationException(
                    "MORPH0042 changed a previous path that skips init.");
            }
        }

        private static void VerifyRuntimeLifecycle(
            TestMapper mapper,
            MappingContext context)
        {
            var typed =
                (ITypeMapper<Source, RuntimeLifecycleDestination>)mapper;

            ExpectConfiguration(() => typed.Create(
                new Source { Value = 43 },
                context));

            if (TestMapper.RuntimeFactoryCalls != 1 ||
                TestMapper.RuntimeRuleReads != 0)
            {
                throw new InvalidOperationException(
                    "Runtime MORPH0042 recovery did not preserve its phase.");
            }

            var nullResult = typed.Create(
                new Source { ReturnNull = true },
                context);

            if (nullResult is not null ||
                TestMapper.RuntimeFactoryCalls != 2 ||
                TestMapper.RuntimeRuleReads != 0)
            {
                throw new InvalidOperationException(
                    "Runtime null did not terminate before MORPH0042.");
            }

            var previous = new RuntimeLifecycleDestination { Value = 47 };
            var updated = typed.Update(new Source(), previous, context);

            if (!ReferenceEquals(previous, updated) ||
                TestMapper.RuntimeFactoryCalls != 2 ||
                TestMapper.RuntimeRuleReads != 0)
            {
                throw new InvalidOperationException(
                    "ConstructUsing previous path did not skip init.");
            }
        }

        private static void VerifyNullPlan(
            TestMapper mapper,
            MappingContext context)
        {
            var typed = (ITypeMapper<Source, NullPlanDestination>)mapper;
            var valid = typed.Create(
                new Source { Value = 53 },
                context);

            if (valid.Value != 53 || TestMapper.NullPlanRuleReads != 1)
            {
                throw new InvalidOperationException(
                    "A valid MORPH0043 sibling branch was changed.");
            }

            var previous = new NullPlanDestination { Value = 59 };
            ExpectConfiguration(() => typed.Update(
                new Source { UseInvalid = true },
                previous,
                context));

            if (previous.Value != 59 || TestMapper.NullPlanRuleReads != 1)
            {
                throw new InvalidOperationException(
                    "MORPH0043 recovery partially evaluated or mutated " +
                    "the invalid terminal leaf.");
            }
        }

        private static void VerifyConditionalLifecycle(
            TestMapper mapper,
            MappingContext context)
        {
            var typed =
                (ITypeMapper<Source, ConditionalLifecycleDestination>)mapper;

            ExpectConfiguration(() => typed.Create(new Source(), context));

            if (ConditionalLifecycleDestination.ConstructorCalls != 0 ||
                TestMapper.ConditionalLifecycleRuleReads != 0)
            {
                throw new InvalidOperationException(
                    "A result-dependent creation condition ran before " +
                    "MORPH0042 recovery.");
            }

            var skipped = new ConditionalLifecycleDestination
            {
                Initial = 61,
                Value = 1
            };
            var applied = new ConditionalLifecycleDestination
            {
                Initial = 67,
                Value = -1
            };
            ConditionalLifecycleDestination.Reset();

            var skippedResult = typed.Update(
                new Source { Value = 71 },
                skipped,
                context);
            var appliedResult = typed.Update(
                new Source { Value = 73 },
                applied,
                context);

            if (!ReferenceEquals(skipped, skippedResult) ||
                skipped.Initial != 61 ||
                skipped.Value != 1 ||
                !ReferenceEquals(applied, appliedResult) ||
                applied.Initial != 67 ||
                applied.Value != 73 ||
                ConditionalLifecycleDestination.ConstructorCalls != 0 ||
                TestMapper.ConditionalLifecycleRuleReads != 0)
            {
                throw new InvalidOperationException(
                    "Result-dependent member control flow lost a valid " +
                    "existing-destination branch: " +
                    $"skipped=({skipped.Initial},{skipped.Value}), " +
                    $"applied=({applied.Initial},{applied.Value}), " +
                    $"constructors={ConditionalLifecycleDestination.ConstructorCalls}, " +
                    $"reads={TestMapper.ConditionalLifecycleRuleReads}.");
            }
        }

        private static void ExpectConfiguration(Action action)
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
                "An invalid member path did not use typed recovery.");
        }
    }
}
