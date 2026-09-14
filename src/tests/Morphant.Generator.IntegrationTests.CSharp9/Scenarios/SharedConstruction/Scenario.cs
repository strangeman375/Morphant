#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SharedConstruction
{
    public sealed class Source
    {
        public List<string> Events { get; } = new List<string>();
        public bool BeforeResult { get; set; }
        public bool AfterResult { get; set; }
        public int ThrowAt { get; set; }
        public string Name { get { Events.Add("name"); return "mapped"; } }
        public bool Before() { Events.Add("before"); return BeforeResult; }
        public bool After() { Events.Add("after"); return AfterResult; }
        public int Read(int index)
        {
            Events.Add("arg" + index);
            if (index == ThrowAt) throw new InvalidOperationException("argument failed");
            return index;
        }
    }

    public sealed class Destination
    {
        private readonly List<string> _events;
        private string _name = "initial";
        public Destination(Source source, int a, int b, int c, int d, int e, int f, int g, int h, int i)
        {
            _events = source.Events;
            _events.Add("constructor");
            Sum = a + b + c + d + e + f + g + h + i;
        }
        public int Sum { get; }
        public string Name
        {
            get => _name;
            init { _events.Add("initializer"); _name = value; }
        }
    }

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Resolve((source, previous) =>
                {
                    if (source.Before() && previous.HasValue && source.After())
                        return previous.Value;
                    return new(source, source.Read(1), source.Read(2), source.Read(3), source.Read(4),
                        source.Read(5), source.Read(6), source.Read(7), source.Read(8), source.Read(9));
                });
    }

    public sealed class CounterSource
    {
        public int Id { get; init; }
    }

    public sealed class CounterDestination
    {
        public CounterDestination(int id, int after) { Id = id; After = after; }
        public int Id { get; }
        public int After { get; }
    }

    [MorphantMapper]
    public partial class CounterMapper : TypeMapper<CounterMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<CounterSource, CounterDestination>()
                .Resolve((source, previous) =>
                {
                    var id = source.Id;
                    Func<int> next = () => ++id;
                    if (previous.HasValue && previous.Value.Id == next())
                        return previous.Value;
                    return new(next(), id);
                });
    }

    public sealed class SeedSource
    {
        public int Id { get; init; }
        public bool Reuse { get; init; }
    }

    public sealed class SeedDestination
    {
        public SeedDestination(int id) => Id = id;
        public int Id { get; }
    }

    [MorphantMapper]
    public partial class SeedMapper : TypeMapper<SeedMapper>
    {
        private int __Construct() => 0;

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<SeedSource, SeedDestination>()
                .Resolve((source, previous) =>
                {
                    var seed = previous.HasValue ? previous.Value.Id : 0;
                    if (previous.HasValue && source.Reuse)
                        return previous.Value;
                    return new(seed + source.Id);
                });
    }

    public sealed class NamedSource
    {
        public string? Name { get; init; }
    }

    public sealed class NamedDestination
    {
        public NamedDestination(string name) => Name = name;
        public string Name { get; }
    }

    [MorphantMapper]
    public partial class NamedMapper : TypeMapper<NamedMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<NamedSource, NamedDestination>()
                .Resolve((source, previous) =>
                {
                    var name = source.Name;
                    if (name is null)
                        throw new InvalidOperationException("missing name");
                    if (previous.HasValue && previous.Value.Name == name)
                        return previous.Value;
                    return new(name);
                });
    }

    public static class Scenario
    {
        public static void VerifyOrder(int operation, bool before, bool after, int throwAt)
        {
            var source = new Source { BeforeResult = before, AfterResult = after, ThrowAt = throwAt };
            var previous = new Destination(source, 0, 0, 0, 0, 0, 0, 0, 0, 0) { Name = "previous" };
            source.Events.Clear();
            var mapper = (ITypeMapper<Source, Destination>)new Mapper();
            Destination? result = null;
            string? failure = null;
            try
            {
                result = operation == 0 ? mapper.Create(source, default) :
                    mapper.Update(source, operation == 1 ? null : previous, default);
            }
            catch (InvalidOperationException exception) { failure = exception.Message; }

            var expected = new List<string> { "before" };
            if (operation == 2 && before) expected.Add("after");
            var reuse = operation == 2 && before && after;
            if (!reuse)
            {
                for (var index = 1; index <= (throwAt == 0 ? 9 : throwAt); index++) expected.Add("arg" + index);
                if (throwAt == 0) expected.AddRange(new[] { "constructor", "name", "initializer" });
            }

            if (string.Join(",", source.Events) != string.Join(",", expected))
                throw new InvalidOperationException("Construction changed evaluation order: " + string.Join(",", source.Events));
            if (reuse)
            {
                if (!ReferenceEquals(result, previous) || result!.Name != "previous" || failure is not null)
                    throw new InvalidOperationException("Reuse evaluated a construction branch.");
            }
            else if (throwAt != 0)
            {
                if (failure != "argument failed" || result is not null)
                    throw new InvalidOperationException("Argument failure did not terminate construction.");
            }
            else if (result is null || ReferenceEquals(result, previous) || result.Sum != 45 || result.Name != "mapped")
                throw new InvalidOperationException("Construction returned the wrong destination.");
        }

        public static void VerifyCapturedStorage(int operation, bool reuse)
        {
            var mapper = (ITypeMapper<CounterSource, CounterDestination>)new CounterMapper();
            var source = new CounterSource { Id = 10 };
            var previous = new CounterDestination(reuse ? 11 : 99, -1);
            var result = operation == 0 ? mapper.Create(source, default) :
                mapper.Update(source, operation == 1 ? null : previous, default);
            if (operation == 2 && reuse)
            {
                if (!ReferenceEquals(result, previous)) throw new InvalidOperationException("Expected reuse.");
            }
            else
            {
                var expected = operation == 2 ? 12 : 11;
                if (result.Id != expected || result.After != expected)
                    throw new InvalidOperationException("The helper copied a captured variable instead of sharing its storage.");
            }
        }

        public static void VerifyPrevious(int operation, bool reuse)
        {
            var mapper = (ITypeMapper<SeedSource, SeedDestination>)new SeedMapper();
            var source = new SeedSource { Id = 5, Reuse = reuse };
            var previous = new SeedDestination(100);
            var result = operation == 0 ? mapper.Create(source, default) :
                mapper.Update(source, operation == 1 ? null : previous, default);
            var expected = operation == 2 ? (reuse ? 100 : 105) : 5;
            if (result.Id != expected || ReferenceEquals(result, previous) != (operation == 2 && reuse))
                throw new InvalidOperationException("Construction lost the previous-dependent local value.");
        }

        public static void VerifyNullable(int operation, string? name)
        {
            var mapper = (ITypeMapper<NamedSource, NamedDestination>)new NamedMapper();
            var source = new NamedSource { Name = name };
            var previous = new NamedDestination("previous");
            NamedDestination? result = null;
            string? failure = null;
            try
            {
                result = operation == 0 ? mapper.Create(source, default) :
                    mapper.Update(source, operation == 1 ? null : previous, default);
            }
            catch (InvalidOperationException exception) { failure = exception.Message; }
            if (name is null)
            {
                if (failure != "missing name" || result is not null)
                    throw new InvalidOperationException("The null guard was lost.");
            }
            else if (result?.Name != name || failure is not null ||
                     ReferenceEquals(result, previous) != (operation == 2 && name == "previous"))
                throw new InvalidOperationException("The narrowed local was not preserved.");
        }
    }
}
