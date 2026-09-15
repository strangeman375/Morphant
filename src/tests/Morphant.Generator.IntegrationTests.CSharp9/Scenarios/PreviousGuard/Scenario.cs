#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PreviousGuard
{
    public sealed class ProbeException : Exception { }

    public sealed class Source
    {
        public readonly List<string> Events = new List<string>();
        public bool Reuse;
        public bool Throw;
        public bool Gate;
        public Destination Seed = null!;
        public Destination? Supplied;
        public Destination? ExpectedProbe;
        public Destination Initial() { Events.Add("initial"); return Seed; }
        public bool Enter() { Events.Add("gate"); return Gate; }
        public bool Decide(Destination? value)
        {
            if (!ReferenceEquals(value, ExpectedProbe)) throw new InvalidOperationException("The guard received another destination.");
            Events.Add("probe:" + (value?.Name ?? "none"));
            if (Throw) throw new ProbeException();
            return Reuse;
        }
        public string BuildName(Destination? value) { Events.Add("build:" + (value?.Name ?? "none")); return "new"; }
        public string MemberName() { Events.Add("member"); return "mapped"; }
        public bool TryGetValue(out Destination? value) { Events.Add("custom"); value = Supplied ?? Seed; return Gate; }
    }

    public sealed class Destination
    {
        private readonly Source _owner;
        private string _name;
        public Destination(Source owner, string name) { _owner = owner; _name = name; owner.Events.Add("construct:" + name); }
        public string Name
        {
            get => _name;
            set { _owner.Events.Add("set:" + value); _name = value; }
        }
    }

    [MorphantMapper]
    public partial class AndMapper : TypeMapper<AndMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) =>
            {
                if (previous.TryGetValue(out var current) && source.Decide(current)) return current;
                return new(source, source.BuildName(current));
            })
            .Members(source => new() { Name = source.MemberName() });
    }

    [MorphantMapper]
    public partial class NegatedMapper : TypeMapper<NegatedMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) =>
            {
                if (!previous.TryGetValue(out var current) || !source.Decide(current))
                    return new(source, source.BuildName(current));
                return current;
            })
            .Members(source => new() { Name = source.MemberName() });
    }

    [MorphantMapper]
    public partial class ExistingMapper : TypeMapper<ExistingMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) =>
            {
                Destination? current = source.Initial();
                if (previous.TryGetValue(out current) && source.Decide(current)) return previous.Value;
                return new(source, source.BuildName(current));
            })
            .Members(source => new() { Name = source.MemberName() });
    }

    [MorphantMapper]
    public partial class ConditionalMapper : TypeMapper<ConditionalMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) =>
            {
                Destination? current = source.Initial();
                if (source.Enter() && previous.TryGetValue(out current) && source.Decide(current)) return previous.Value;
                return new(source, source.BuildName(current));
            })
            .Members(source => new() { Name = source.MemberName() });
    }

    [MorphantMapper]
    public partial class StoredMapper : TypeMapper<StoredMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) =>
            {
                var found = previous.TryGetValue(out var current);
                if (found && source.Decide(current)) return previous.Value;
                return new(source, source.BuildName(current));
            })
            .Members(source => new() { Name = source.MemberName() });
    }

    [MorphantMapper]
    public partial class ArbitraryMapper : TypeMapper<ArbitraryMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) =>
            {
                if (source.TryGetValue(out var current) && previous.HasValue && source.Decide(current)) return previous.Value;
                return new(source, source.BuildName(current));
            })
            .Members(source => new() { Name = source.MemberName() });
    }

    [MorphantMapper]
    public partial class MembersConstructMapper : TypeMapper<MembersConstructMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Construct(source => new(source, source.BuildName(null)))
            .Members((source, previous, result) =>
            {
                if (previous.TryGetValue(out var current) && source.Decide(current))
                    return new() { Name = current.Name + ":reused" };
                return new() { Name = source.MemberName() };
            });
    }

    [MorphantMapper]
    public partial class MembersResolveMapper : TypeMapper<MembersResolveMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) => new(source, source.BuildName(null)))
            .Members((source, previous, result) =>
            {
                if (previous.TryGetValue(out var current) && source.Decide(current))
                    return new() { Name = current.Name + ":reused" };
                return new() { Name = source.MemberName() };
            });
    }

    public static class Scenario
    {
        public static void Verify(string form, string operation, bool reuse, bool throwProbe, bool gate)
        {
            var source = new Source { Reuse = reuse, Throw = throwProbe, Gate = gate };
            source.Seed = new Destination(source, "seed");
            var previous = operation == "UpdateExisting" ? new Destination(source, "old") : null;
            source.Supplied = previous;
            source.ExpectedProbe = previous;
            source.Events.Clear();
            ITypeMapper<Source, Destination> mapper = form switch
            {
                "And" => new AndMapper(), "Negated" => new NegatedMapper(),
                "Existing" => new ExistingMapper(), "Conditional" => new ConditionalMapper(),
                "Stored" => new StoredMapper(), "Arbitrary" => new ArbitraryMapper(),
                "MembersConstruct" => new MembersConstructMapper(), "MembersResolve" => new MembersResolveMapper(),
                _ => throw new ArgumentOutOfRangeException(nameof(form))
            };
            bool members = form.StartsWith("Members", StringComparison.Ordinal);
            bool probes = previous is not null && (form is not ("Conditional" or "Arbitrary") || gate);
            bool reused = members ? previous is not null && form == "MembersConstruct" : probes && reuse;
            var expected = new List<string>();
            if (form is "Existing" or "Conditional") expected.Add("initial");
            if (form == "Conditional") expected.Add("gate");
            if (form == "Arbitrary") expected.Add("custom");
            if (members && !reused) { expected.Add("build:none"); expected.Add("construct:new"); }
            if (probes) expected.Add("probe:old");
            if (!(probes && throwProbe))
            {
                if (!members && !reused)
                {
                    var value = form == "Conditional" && !gate || form == "Arbitrary" && previous is null
                        ? "seed" : previous is null ? "none" : "old";
                    expected.Add("build:" + value);
                    expected.Add("construct:new");
                }
                if (members && probes && reuse) expected.Add("set:old:reused");
                else { expected.Add("member"); expected.Add("set:mapped"); }
            }
            Destination? result = null;
            bool threw = false;
            try { result = operation == "Create" ? mapper.Create(source) : mapper.Update(source, previous); }
            catch (ProbeException) { threw = true; }
            if (threw != (probes && throwProbe) || string.Join(",", source.Events) != string.Join(",", expected))
                throw new InvalidOperationException("Guard evaluation changed. Expected " + string.Join(",", expected) + "; actual " + string.Join(",", source.Events));
            if (threw)
            {
                if (previous?.Name != (previous is null ? null : "old")) throw new InvalidOperationException("The guard mutated the destination before throwing.");
                return;
            }
            if (ReferenceEquals(result, previous) != reused || result?.Name != (members && probes && reuse ? "old:reused" : "mapped"))
                throw new InvalidOperationException("Reuse or member binding changed.");
            if (!reused && previous is not null && previous.Name != "old") throw new InvalidOperationException("Replacement mutated the previous destination.");
        }
    }
}
