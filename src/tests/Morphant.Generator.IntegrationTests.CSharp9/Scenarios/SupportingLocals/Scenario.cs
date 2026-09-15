#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SupportingLocals
{
    public sealed class WriteException : Exception { }
    public sealed class Source
    {
        public readonly List<string> Events = new List<string>();
        public Action? Mutate;
        public bool Throw;
        public int Conversions;
        public string Initial() { Events.Add("initial"); return "initial"; }
        public bool Remember(Action action) { Events.Add("remember"); Mutate = action; return true; }
    }
    public sealed class Destination
    {
        private readonly Source _source;
        private string _name;
        private string _label = "unset";
        public Destination(Source owner, string name) { _source = owner; _name = name; owner.Events.Add("construct"); }
        public string Name
        {
            get { _source.Events.Add("read"); return _name; }
            set { _source.Events.Add("name:" + value); if (_source.Throw) throw new WriteException(); _name = value; _source.Mutate?.Invoke(); }
        }
        public string Label { get => _label; set { _source.Events.Add("label:" + value); _label = value; } }
        public string ReadName() => _name;
    }
    public readonly struct Input
    {
        private readonly Source _source;
        private readonly string _value;
        public Input(Source source, string value) { _source = source; _value = value; }
        public static implicit operator string(Input value)
        {
            var count = ++value._source.Conversions;
            value._source.Events.Add("convert:" + count);
            return value._value + ":" + count;
        }
    }
    public sealed class BoxedDestination
    {
        public BoxedDestination(object first) { First = first; }
        public object First { get; set; }
        public object Second { get; set; } = 0;
    }
    [MorphantMapper]
    public partial class StableConstructMapper : TypeMapper<StableConstructMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Construct(source => new(source, source.Initial()))
            .Members((source, previous, result) =>
            {
                var normalized = result.Name.ToUpperInvariant();
                string userCopy = normalized;
                return new() { Name = userCopy, Label = normalized };
            });
    }

    [MorphantMapper]
    public partial class StableResolveMapper : TypeMapper<StableResolveMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) => new(source, source.Initial()))
            .Members((source, previous, result) =>
            {
                var normalized = result.Name.ToUpperInvariant();
                string userCopy = normalized;
                return new() { Name = userCopy, Label = normalized };
            });
    }

    [MorphantMapper]
    public partial class CapturedConstructMapper : TypeMapper<CapturedConstructMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Construct(source => new(source, source.Initial()))
            .Members((source, previous, result) =>
            {
                var normalized = result.Name.ToUpperInvariant();
                var remembered = source.Remember(() => normalized = "changed");
                return new() { Name = normalized, Label = normalized };
            });
    }

    [MorphantMapper]
    public partial class CapturedResolveMapper : TypeMapper<CapturedResolveMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) => new(source, source.Initial()))
            .Members((source, previous, result) =>
            {
                var normalized = result.Name.ToUpperInvariant();
                var remembered = source.Remember(() => normalized = "changed");
                return new() { Name = normalized, Label = normalized };
            });
    }

    [MorphantMapper]
    public partial class ConvertedConstructMapper : TypeMapper<ConvertedConstructMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Construct(source => new(source, source.Initial()))
            .Members((source, previous, result) =>
            {
                var normalized = new Input(source, result.Name.ToUpperInvariant());
                return new() { Name = (string)normalized, Label = (string)normalized };
            });
    }

    [MorphantMapper]
    public partial class ConvertedResolveMapper : TypeMapper<ConvertedResolveMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) => new(source, source.Initial()))
            .Members((source, previous, result) =>
            {
                var normalized = new Input(source, result.Name.ToUpperInvariant());
                return new() { Name = (string)normalized, Label = (string)normalized };
            });
    }

    [MorphantMapper]
    public partial class BoxedConstructMapper : TypeMapper<BoxedConstructMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, BoxedDestination>()
            .Construct(source => new(42))
            .Members((source, previous, result) =>
            {
                var value = (int)result.First;
                return new() { First = value, Second = value };
            });
    }

    [MorphantMapper]
    public partial class BoxedResolveMapper : TypeMapper<BoxedResolveMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, BoxedDestination>()
            .Resolve((source, previous) => new(42))
            .Members((source, previous, result) =>
            {
                var value = (int)result.First;
                return new() { First = value, Second = value };
            });
    }
    public static class Scenario
    {
        public static void Verify(string form, bool resolve, int operation, bool throwWrite)
        {
            var source = new Source { Throw = throwWrite };
            if (form == "Boxed")
            {
                ITypeMapper<Source, BoxedDestination> boxed = resolve ? new BoxedResolveMapper() : new BoxedConstructMapper();
                var old = new BoxedDestination(100);
                var value = operation == 0 ? boxed.Create(source) : boxed.Update(source, operation == 1 ? null : old);
                var reused = operation == 2 && !resolve;
                if ((int)value.First != (reused ? 100 : 42) || (int)value.Second != (int)value.First ||
                    ReferenceEquals(value.First, value.Second) || ReferenceEquals(value, old) != reused)
                    throw new InvalidOperationException("Boxing or destination reuse changed.");
                return;
            }
            ITypeMapper<Source, Destination> mapper = form switch
            {
                "Stable" => resolve ? new StableResolveMapper() : new StableConstructMapper(),
                "Captured" => resolve ? new CapturedResolveMapper() : new CapturedConstructMapper(),
                "Converted" => resolve ? new ConvertedResolveMapper() : new ConvertedConstructMapper(),
                _ => throw new ArgumentOutOfRangeException(nameof(form))
            };
            var previous = new Destination(source, "old");
            source.Events.Clear();
            bool reuse = operation == 2 && !resolve;
            string expectedValue = reuse ? "OLD" : "INITIAL";
            var expected = new List<string>();
            if (!reuse) expected.AddRange(new[] { "initial", "construct" });
            expected.Add("read");
            if (form == "Captured") expected.Add("remember");
            if (form == "Converted") expected.AddRange(new[] { "convert:1", "convert:2" });
            var name = expectedValue + (form == "Converted" ? ":1" : "");
            var label = expectedValue + (form == "Converted" ? ":2" : "");
            expected.Add("name:" + name);
            if (!throwWrite) expected.Add("label:" + label);
            Destination? result = null;
            bool threw = false;
            try { result = operation == 0 ? mapper.Create(source) : mapper.Update(source, operation == 1 ? null : previous); }
            catch (WriteException) { threw = true; }
            if (threw != throwWrite || string.Join(",", source.Events) != string.Join(",", expected))
                throw new InvalidOperationException("Evaluation order changed. Expected " + string.Join(",", expected) + "; actual " + string.Join(",", source.Events));
            if (!threw && (result?.ReadName() != name || result.Label != label || ReferenceEquals(result, previous) != reuse))
                throw new InvalidOperationException("A supporting copy changed the final member values.");
        }
    }
}
