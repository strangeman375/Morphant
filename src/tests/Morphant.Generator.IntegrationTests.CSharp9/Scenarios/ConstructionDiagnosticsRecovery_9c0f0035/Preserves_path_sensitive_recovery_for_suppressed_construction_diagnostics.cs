// Compiled integration scenario: ConstructionDiagnosticsTests::Preserves_path_sensitive_recovery_for_suppressed_construction_diagnostics
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0035
#pragma warning disable MORPH0036
#pragma warning disable MORPH0037
#pragma warning disable MORPH0038
#pragma warning disable MORPH0039

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConstructionDiagnosticsRecovery_9c0f0035
{
    public sealed class Source
    {
        public int Value { get; init; }

        public bool UseInvalid { get; init; }
    }

    public interface IMissingDestination
    {
        int Value { get; set; }
    }

    public sealed class MissingDestination : IMissingDestination
    {
        public int Value { get; set; }
    }

    public sealed class ConventionDestination
    {
        public int Value { get; set; }
    }

    public sealed class RuleDestination
    {
        public RuleDestination(object value) => Value = value;

        public object Value { get; set; }
    }

    public sealed class PreviousDestination
    {
        public PreviousDestination(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class NullPlanDestination
    {
        public NullPlanDestination(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class RuntimeSource
    {
        public static int Reads { get; private set; }

        public int Value
        {
            get
            {
                Reads++;
                return 91;
            }
        }
    }

    public sealed class RuntimeNullDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        public static int InvalidRuleReads { get; private set; }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, IMissingDestination>();
            builder.Map<Source, ConventionDestination>()
                .ConstructorSelection(ConstructorSelection.Explicit);
            builder.Map<Source, RuleDestination>()
                .Construct(source => new(Value<int>(ReadInvalidRule())));
            builder.Map<Source, PreviousDestination>()
                .Resolve((source, previous) =>
                {
                    if (source.UseInvalid)
                        return previous;

                    return new(source.Value);
                });
            builder.Map<Source, NullPlanDestination>()
                .Resolve((source, previous) =>
                {
                    if (source.UseInvalid)
                        return default!;

                    if (previous.HasValue)
                        return previous;

                    return new(source.Value);
                });
            builder.Map<RuntimeSource, RuntimeNullDestination>()
                .ConstructUsing(source => null!)
                .Members(source => new() { Value = source.Value });
        }

        private static int ReadInvalidRule()
        {
            InvalidRuleReads++;
            return 73;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var source = new Source { Value = 17 };

            var missing = new MissingDestination();
            var missingMapper =
                (ITypeMapper<Source, IMissingDestination>)mapper;
            var missingUpdated = missingMapper.Update(
                source,
                missing,
                context);

            if (!ReferenceEquals(missing, missingUpdated) ||
                missing.Value != 17)
            {
                throw new InvalidOperationException(
                    "MORPH0035 recovery changed existing Update.");
            }

            ExpectConfiguration(() => missingMapper.Create(source, context));
            ExpectConfiguration(() =>
                missingMapper.Update(source, null, context));

            var convention = new ConventionDestination();
            var conventionMapper =
                (ITypeMapper<Source, ConventionDestination>)mapper;
            var conventionUpdated = conventionMapper.Update(
                source,
                convention,
                context);

            if (!ReferenceEquals(convention, conventionUpdated) ||
                convention.Value != 17)
            {
                throw new InvalidOperationException(
                    "MORPH0036 recovery changed existing Update.");
            }

            ExpectConfiguration(() =>
                conventionMapper.Create(source, context));

            var rule = new RuleDestination(0);
            var ruleMapper = (ITypeMapper<Source, RuleDestination>)mapper;
            var ruleUpdated = ruleMapper.Update(source, rule, context);

            if (!ReferenceEquals(rule, ruleUpdated) ||
                !Equals(rule.Value, 17) ||
                TestMapper.InvalidRuleReads != 0)
            {
                throw new InvalidOperationException(
                    "MORPH0037 recovery evaluated an invalid rule or " +
                    "changed existing Update.");
            }

            ExpectConfiguration(() => ruleMapper.Create(source, context));

            if (TestMapper.InvalidRuleReads != 0)
            {
                throw new InvalidOperationException(
                    "The invalid constructor rule was evaluated.");
            }

            var previousMapper =
                (ITypeMapper<Source, PreviousDestination>)mapper;
            var previous = new PreviousDestination(31);
            var reused = previousMapper.Update(
                new Source { UseInvalid = true },
                previous,
                context);
            var replacement = previousMapper.Update(
                new Source { Value = 47 },
                previous,
                context);

            if (!ReferenceEquals(previous, reused) ||
                ReferenceEquals(previous, replacement) ||
                replacement.Value != 47)
            {
                throw new InvalidOperationException(
                    "MORPH0038 recovery changed a reachable valid branch.");
            }

            ExpectConfiguration(() => previousMapper.Create(
                new Source { UseInvalid = true },
                context));

            var nullPlanMapper =
                (ITypeMapper<Source, NullPlanDestination>)mapper;
            var constructed = nullPlanMapper.Create(source, context);
            var retained = new NullPlanDestination(53);
            var retainedResult = nullPlanMapper.Update(
                source,
                retained,
                context);

            if (constructed.Value != 17 ||
                !ReferenceEquals(retained, retainedResult))
            {
                throw new InvalidOperationException(
                    "MORPH0039 recovery changed a non-null branch.");
            }

            ExpectConfiguration(() => nullPlanMapper.Create(
                new Source { UseInvalid = true },
                context));
            ExpectConfiguration(() => nullPlanMapper.Update(
                new Source { UseInvalid = true },
                retained,
                context));

            var runtimeResult =
                ((ITypeMapper<RuntimeSource, RuntimeNullDestination>)mapper)
                .Create(new RuntimeSource(), context);

            if (runtimeResult is not null || RuntimeSource.Reads != 0)
            {
                throw new InvalidOperationException(
                    "Runtime null was analyzed as a structured null plan " +
                    "or did not stop Members.");
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
                "An invalid construction path did not use typed recovery.");
        }
    }
}
