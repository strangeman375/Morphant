#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0042 // Exercise the generated recovery after suppressing diagnostics.
using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConstructorResultDependencies
{
    public enum Route { Unguarded, Explicit, Operation, Previous, Local, Deferred, Numeric, Resolve, PreviousValue, Factory, ResolveFactory, Unrelated }
    public sealed class Source
    {
        public int Value => 7;
        public bool Replace { get; set; }
        public int Reads { get; private set; }
        public int Read(Func<int> get) { Reads++; return get(); }
    }
    public sealed class Destination
    {
        private int value;
        public static int Constructions { get; set; }
        public Destination(int value)
        {
            Constructions++;
            ConstructorValue = value;
            Value = value + 100;
        }
        public int ConstructorValue { get; }
        public int Writes { get; private set; }
        public int Value { get => value; set { this.value = value; Writes++; } }
        public int Echo { get; set; }
    }
    [MorphantMapper]
    public partial class UnguardedMapper : TypeMapper<UnguardedMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Members((_, _, result) => new() { Value = result.Value + 10 });
    }
    [MorphantMapper]
    public partial class ExplicitMapper : TypeMapper<ExplicitMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Construct(_ => new(7))
            .Members((_, _, result) => new() { Value = result.Value + 10 });
    }
    [MorphantMapper]
    public partial class OperationMapper : TypeMapper<OperationMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Members((_, _, result, context) => new() { Value = context.Operation is MappingOperation.Create ? 7 : result.Value + 10 });
    }
    [MorphantMapper]
    public partial class PreviousMapper : TypeMapper<PreviousMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Members((_, previous, result) => new() { Value = previous.HasValue ? result.Value + 10 : 7 });
    }
    [MorphantMapper]
    public partial class LocalMapper : TypeMapper<LocalMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Members((_, previous, result, context) =>
            {
                var hasResult = context.Operation != MappingOperation.Create && previous.HasValue;
                var value = hasResult ? result.Value + 10 : 7;
                return new() { Value = value };
            });
    }
    [MorphantMapper]
    public partial class DeferredMapper : TypeMapper<DeferredMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Members((source, previous, result) => new()
            {
                Value = previous.HasValue ? result.Value + 10 : source.Read(() =>
                {
                    var select = true;
                    select = false;
                    return select ? 99 : 7;
                })
            });
    }
    [MorphantMapper]
    public partial class NumericMapper : TypeMapper<NumericMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Members((_, previous, result) =>
            {
                var left = double.NaN;
                var right = double.NaN;
                var one = 1;
                var longOne = 1L;
                var value = left == right || one != longOne ? 99 : 7;
                return new() { Value = previous.HasValue ? result.Value + 10 : value };
            });
    }
    [MorphantMapper]
    public partial class ResolveMapper : TypeMapper<ResolveMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) => { if (previous.HasValue && !source.Replace) return previous; return new(7); })
            .Members((_, previous, result) => new() { Value = previous.HasValue ? result.Value + 10 : 7 });
    }
    [MorphantMapper]
    public partial class PreviousValueMapper : TypeMapper<PreviousValueMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) => { if (previous.HasValue && !source.Replace) return previous; return new(7); })
            .Members((_, previous) => new() { Value = previous.HasValue ? previous.Value.Value + 10 : 7 });
    }
    [MorphantMapper]
    public partial class FactoryMapper : TypeMapper<FactoryMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .ConstructUsing(_ => new Destination(7))
            .Members((_, _, result) => new() { Value = result.Value + 10 });
    }
    [MorphantMapper]
    public partial class ResolveFactoryMapper : TypeMapper<ResolveFactoryMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .ResolveUsing((source, previous) => previous.HasValue && !source.Replace ? previous.Value : new Destination(7))
            .Members((_, _, result) => new() { Value = result.Value + 10 });
    }
    [MorphantMapper]
    public partial class UnrelatedMapper : TypeMapper<UnrelatedMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Members((_, _, result) => new() { Value = 7, Echo = result.Value + 10 });
    }
    public static class Scenario
    {
        public static void Verify(Route route, bool update, bool hasPrevious, bool replace = false)
        {
            ITypeMapper<Source, Destination> mapper = route switch
            {
                Route.Unguarded => new UnguardedMapper(),
                Route.Explicit => new ExplicitMapper(),
                Route.Operation => new OperationMapper(),
                Route.Previous => new PreviousMapper(),
                Route.Local => new LocalMapper(),
                Route.Deferred => new DeferredMapper(),
                Route.Numeric => new NumericMapper(),
                Route.Resolve => new ResolveMapper(),
                Route.PreviousValue => new PreviousValueMapper(),
                Route.Factory => new FactoryMapper(),
                Route.ResolveFactory => new ResolveFactoryMapper(),
                Route.Unrelated => new UnrelatedMapper(),
                _ => throw new ArgumentOutOfRangeException(nameof(route))
            };
            var source = new Source { Replace = replace };
            var previous = hasPrevious ? new Destination(10) : null;
            var before = Destination.Constructions;
            var failure = !hasPrevious && (route is Route.Unguarded or Route.Explicit || route == Route.Operation && update) ||
                hasPrevious && replace && route == Route.Resolve;
            try
            {
                var result = update ? mapper.Update(source, previous) : mapper.Create(source);
                Check(!failure, "An invalid constructor dependency did not throw.");
                var replaced = hasPrevious && replace && route is Route.PreviousValue or Route.ResolveFactory;
                Check(ReferenceEquals(previous, result) == (hasPrevious && !replaced), "Unexpected destination identity.");
                var factory = route is Route.Factory or Route.ResolveFactory;
                var expectedValue = route == Route.Unrelated && hasPrevious ? 7 :
                    replaced && route == Route.PreviousValue ? 220 :
                    hasPrevious && !replaced ? 120 : factory ? 117 : 107;
                Check(result.Value == expectedValue, "Wrong destination value.");
                Check(result.Writes == (hasPrevious && !replaced || factory ? 2 : 1), "Member assigned an incorrect number of times.");
                if (!hasPrevious || replaced)
                    Check(result.ConstructorValue == (replaced && route == Route.PreviousValue ? 120 : 7), "Wrong constructor argument.");
                if (route == Route.Unrelated)
                    Check(result.Echo == result.Value + 10, "Unrelated result-dependent setter did not run after construction.");
                if (route == Route.Deferred)
                    Check(source.Reads == (hasPrevious ? 0 : 1), "Deferred value was evaluated an incorrect number of times.");
                if (replaced) Check(previous!.Value == 110 && previous.Writes == 1, "Replacement mutated the previous object.");
            }
            catch (MappingConfigurationException exception)
            {
                Check(failure, "A valid path failed.");
                Check(exception.Operation == (update ? MappingOperation.Update : MappingOperation.Create) &&
                    exception.SourceType == typeof(Source) && exception.DestinationType == typeof(Destination),
                    "Failure lost the requested operation or pair.");
                Check(Destination.Constructions == before, "The invalid path invoked a constructor.");
                if (previous is not null) Check(previous.Value == 110 && previous.Writes == 1, "The invalid path mutated previous.");
            }
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
