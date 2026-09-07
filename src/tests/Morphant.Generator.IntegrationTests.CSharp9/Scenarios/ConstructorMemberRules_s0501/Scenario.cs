#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConstructorMemberRules_s0501
{
    public enum ConstructionRoute { Automatic, ByConvention, Explicit, Resolve }

    public sealed class Source
    {
        public bool Enabled { get; set; } = true;
        public bool ThrowRule { get; set; }
        public int Reads { get; private set; }
        public int RuleCalls { get; private set; }
        public int Value { get { Reads++; return 7; } }

        public int ReadRule()
        {
            RuleCalls++;
            if (ThrowRule) throw new RuleException();
            return Value + 10;
        }
    }

    public sealed class RuleException : Exception { }

    public interface IObservedDestination
    {
        int Value { get; }
        int ConstructorValue { get; }
        int Writes { get; }
    }

    public class MutableDestination : IObservedDestination
    {
        private int value;
        public MutableDestination(int value) { ConstructorValue = value; Value = value; }
        public int ConstructorValue { get; }
        public int Writes { get; private set; }
        public int Value { get => value; set { this.value = value; Writes++; } }
    }

    public sealed class InitDestination : IObservedDestination
    {
        private readonly int value;
        public InitDestination(int value) { ConstructorValue = value; Value = value; }
        public int ConstructorValue { get; }
        public int Writes { get; private set; }
        public int Value { get => value; init { this.value = value; Writes++; } }
    }

    public sealed class BranchDestination : MutableDestination
    {
        public BranchDestination(int value) : base(value) { }
        public int Echo { get; set; }
    }

    public sealed class ConventionDestination : IObservedDestination
    {
        private int value;
        public ConventionDestination(int value) { ConstructorValue = value; Value = value + 100; }
        public int ConstructorValue { get; }
        public int Writes { get; private set; }
        public int Value { get => value; set { this.value = value; Writes++; } }
    }

    [MorphantMapper]
    public partial class AutomaticMapper : TypeMapper<AutomaticMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, MutableDestination>()
                .Members(source => new() { Value = source.ReadRule() });
            builder.Map<Source, InitDestination>()
                .Members(source => new() { Value = source.ReadRule() });
            builder.Map<Source, BranchDestination>().Members(source =>
            {
                if (!source.Enabled) return new() { Value = Ignore(), Echo = Ignore() };
                var value = source.ReadRule();
                return new() { Value = value, Echo = value + 3 };
            });
            builder.Map<Source, ConventionDestination>();
        }
    }

    [MorphantMapper]
    public partial class ConventionMapper : TypeMapper<ConventionMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, MutableDestination>().Construct(_ => new(ByConvention()))
                .Members(source => new() { Value = source.ReadRule() });
            builder.Map<Source, InitDestination>().Construct(_ => new(ByConvention()))
                .Members(source => new() { Value = source.ReadRule() });
            builder.Map<Source, BranchDestination>().Construct(_ => new(ByConvention())).Members(source =>
            {
                if (!source.Enabled) return new() { Value = Ignore(), Echo = Ignore() };
                var value = source.ReadRule();
                return new() { Value = value, Echo = value + 3 };
            });
            builder.Map<Source, ConventionDestination>().Construct(_ => new(ByConvention()));
        }
    }

    [MorphantMapper]
    public partial class ExplicitMapper : TypeMapper<ExplicitMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, MutableDestination>().Construct(source => new(source.Value))
                .Members(source => new() { Value = source.ReadRule() });
            builder.Map<Source, InitDestination>().Construct(source => new(source.Value))
                .Members(source => new() { Value = source.ReadRule() });
            builder.Map<Source, BranchDestination>().Construct(source => new(source.Value)).Members(source =>
            {
                if (!source.Enabled) return new() { Value = Ignore(), Echo = Ignore() };
                var value = source.ReadRule();
                return new() { Value = value, Echo = value + 3 };
            });
            builder.Map<Source, ConventionDestination>().Construct(source => new(source.Value));
        }
    }

    [MorphantMapper]
    public partial class ResolveMapper : TypeMapper<ResolveMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, MutableDestination>().Resolve((_, previous) =>
            {
                if (previous.HasValue) return previous;
                return new(ByConvention());
            }).Members(source => new() { Value = source.ReadRule() });
            builder.Map<Source, InitDestination>().Resolve((_, previous) =>
            {
                if (previous.HasValue) return previous;
                return new(ByConvention());
            }).Members(source => new() { Value = source.ReadRule() });
            builder.Map<Source, BranchDestination>().Resolve((_, previous) =>
            {
                if (previous.HasValue) return previous;
                return new(ByConvention());
            }).Members(source =>
            {
                if (!source.Enabled) return new() { Value = Ignore(), Echo = Ignore() };
                var value = source.ReadRule();
                return new() { Value = value, Echo = value + 3 };
            });
            builder.Map<Source, ConventionDestination>().Resolve((_, previous) =>
            {
                if (previous.HasValue) return previous;
                return new(ByConvention());
            });
        }
    }

    public static class Scenario
    {
        public static void VerifyMutable(ConstructionRoute route) => VerifyMapping(
            (ITypeMapper<Source, MutableDestination>)CreateMapper(route),
            () => new MutableDestination(99), creationOnly: false);

        public static void VerifyInit(ConstructionRoute route) => VerifyMapping(
            (ITypeMapper<Source, InitDestination>)CreateMapper(route),
            () => new InitDestination(99), creationOnly: true);

        private static void VerifyMapping<TDestination>(
            ITypeMapper<Source, TDestination> mapper,
            Func<TDestination> existing, bool creationOnly)
            where TDestination : class, IObservedDestination
        {
            var source = new Source();
            var created = mapper.Create(source);
            VerifyCreated(created, source);
            source = new Source();
            VerifyCreated(mapper.Update(source, null), source);

            source = new Source { ThrowRule = creationOnly };
            var previous = existing();
            var updated = mapper.Update(source, previous);
            Equal(true, ReferenceEquals(previous, updated), "Update identity");
            Equal(creationOnly ? 99 : 17, updated.Value, "Update value");
            Equal(99, updated.ConstructorValue, "Update constructor");
            Equal(creationOnly ? 1 : 2, updated.Writes, "Update writes");
            Equal(creationOnly ? 0 : 1, source.Reads, "Update reads");
            Equal(creationOnly ? 0 : 1, source.RuleCalls, "Update rule calls");

            ExpectRuleException(() => mapper.Create(new Source { ThrowRule = true }));
            ExpectRuleException(() => mapper.Update(new Source { ThrowRule = true }, null));
        }

        private static void VerifyCreated(IObservedDestination result, Source source)
        {
            Equal(17, result.Value, "created explicit value");
            Equal(17, result.ConstructorValue, "constructor receives the configured member value");
            Equal(1, result.Writes, "constructor is the only assignment");
            Equal(1, source.Reads, "the configured value is computed once");
            Equal(1, source.RuleCalls, "member expression evaluated once");
        }

        public static void VerifyBranches(ConstructionRoute route)
        {
            var mapper = (ITypeMapper<Source, BranchDestination>)CreateMapper(route);
            foreach (bool enabled in new[] { true, false })
            {
                var source = new Source { Enabled = enabled, ThrowRule = !enabled };
                var result = mapper.Create(source);
                Equal(enabled ? 17 : 7, result.Value, "selected branch value");
                Equal(enabled ? 20 : 0, result.Echo, "shared local value");
                Equal(enabled ? 1 : 0, source.RuleCalls, "selected dependency once");
                Equal(1, source.Reads, "selected reads");
                Equal(enabled ? 17 : 7, result.ConstructorValue, "selected constructor argument");
                Equal(1, result.Writes, "no repeated setter");

                source = new Source { Enabled = enabled, ThrowRule = !enabled };
                var previous = new BranchDestination(99) { Echo = 98 };
                var updated = mapper.Update(source, previous);
                Equal(true, ReferenceEquals(previous, updated), "branch Update identity");
                Equal(enabled ? 17 : 99, updated.Value, "branch Update value");
                Equal(enabled ? 20 : 98, updated.Echo, "branch Update echo");
                Equal(enabled ? 1 : 0, source.RuleCalls, "branch Update calls");
            }
        }

        public static void VerifyAutomaticRule(ConstructionRoute route)
        {
            var mapper = (ITypeMapper<Source, ConventionDestination>)CreateMapper(route);
            var source = new Source();
            var created = mapper.Create(source);
            Equal(107, created.Value, "convention keeps constructor result");
            Equal(1, created.Writes, "no redundant automatic assignment");
            Equal(1, source.Reads, "automatic argument once");
            var previous = new ConventionDestination(99);
            var updated = mapper.Update(new Source(), previous);
            Equal(true, ReferenceEquals(previous, updated), "automatic Update identity");
            Equal(7, updated.Value, "automatic Update still assigns");
        }

        private static object CreateMapper(ConstructionRoute route) => route switch
        {
            ConstructionRoute.Automatic => new AutomaticMapper(),
            ConstructionRoute.ByConvention => new ConventionMapper(),
            ConstructionRoute.Explicit => new ExplicitMapper(),
            ConstructionRoute.Resolve => new ResolveMapper(),
            _ => throw new ArgumentOutOfRangeException(nameof(route))
        };

        private static void ExpectRuleException(Action action)
        {
            try { action(); }
            catch (RuleException) { return; }
            throw new InvalidOperationException("The selected member expression was skipped.");
        }

        private static void Equal<T>(T expected, T actual, string operation)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"{operation}: expected {expected}, got {actual}.");
        }
    }
}
