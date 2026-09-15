#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SupportingValues
{
    public sealed class Source
    {
        public readonly List<string> Events = new List<string>();
        public bool Reuse { get; set; }
        public bool ThrowMember { get; set; }
        private int _reads;
        public int Id { get { Events.Add("id"); return ++_reads; } }
        public string ReadMember()
        {
            Events.Add("member");
            if (ThrowMember) throw new ProbeException();
            return "member:" + ++_reads;
        }
        public Input ReadInput()
        {
            Events.Add("read");
            if (ThrowMember) throw new ProbeException();
            return new Input(this, ++_reads);
        }
    }
    public sealed class ProbeException : Exception { }
    public readonly struct Input
    {
        public Input(Source owner, int number) { Owner = owner; Number = number; }
        public Source Owner { get; }
        public int Number { get; }
        public static explicit operator Payload(Input value)
        {
            value.Owner.Events.Add("convert");
            return new Payload(value.Owner, value.Number);
        }
    }
    public readonly struct Payload
    {
        public Payload(Source owner, int number) { Owner = owner; Number = number; }
        public Source Owner { get; }
        public int Number { get; }
    }
    public sealed class Destination
    {
        private Payload _value;
        public Destination(Payload value)
        {
            _value = new Payload(value.Owner, value.Number + 10);
            value.Owner.Events.Add("construct");
        }
        public Payload Value
        {
            get => _value;
            set { value.Owner.Events.Add("set"); _value = value; }
        }
    }

    [MorphantMapper]
    public partial class ConstructAutomaticMapper : TypeMapper<ConstructAutomaticMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(Auto()))
                .Members(source => new() { Value = (Payload)source.ReadInput() });
    }

    [MorphantMapper]
    public partial class ConstructValueTupleMapper : TypeMapper<ConstructValueTupleMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, (int Id, string Name)>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.Id, Ignore()))
                .Members(source => new() { Name = source.ReadMember() });
    }

    [MorphantMapper]
    public partial class ConstructReferenceTupleMapper : TypeMapper<ConstructReferenceTupleMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Tuple<int, string>>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.Id, Ignore()))
                .Members(source => new() { Item2 = source.ReadMember() });
    }

    [MorphantMapper]
    public partial class ResolveAutomaticMapper : TypeMapper<ResolveAutomaticMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Resolve((source, previous) =>
                {
                    if (previous.HasValue && source.Reuse) return previous.Value;
                    return new(Auto());
                })
                .Members(source => new() { Value = (Payload)source.ReadInput() });
    }

    [MorphantMapper]
    public partial class ResolveValueTupleMapper : TypeMapper<ResolveValueTupleMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, (int Id, string Name)>()
                .MemberSelection(MemberSelection.Explicit)
                .Resolve((source, previous) =>
                {
                    if (previous.HasValue && source.Reuse) return previous.Value;
                    return new(source.Id, Ignore());
                })
                .Members(source => new() { Name = source.ReadMember() });
    }

    [MorphantMapper]
    public partial class ResolveReferenceTupleMapper : TypeMapper<ResolveReferenceTupleMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Tuple<int, string>>()
                .MemberSelection(MemberSelection.Explicit)
                .Resolve((source, previous) =>
                {
                    if (previous.HasValue && source.Reuse) return previous.Value;
                    return new(source.Id, Ignore());
                })
                .Members(source => new() { Item2 = source.ReadMember() });
    }

    public static class Scenario
    {
        public static void VerifyAutomatic(bool resolve, string operation, bool throwMember)
        {
            ITypeMapper<Source, Destination> mapper = resolve ? new ResolveAutomaticMapper() : new ConstructAutomaticMapper();
            var source = new Source { Reuse = operation == "Reuse", ThrowMember = throwMember };
            var previous = operation == "Reuse" || operation == "Replace" ? new Destination(new Payload(source, 40)) : null;
            source.Events.Clear();
            bool reuse = previous is not null && (!resolve || source.Reuse);
            Destination? result = null;
            bool threw = false;
            try { result = operation == "Create" ? mapper.Create(source) : mapper.Update(source, previous); }
            catch (ProbeException) { threw = true; }
            string expected = throwMember ? "read" : "read,convert," + (reuse ? "set" : "construct");
            if (threw != throwMember || string.Join(",", source.Events) != expected)
                throw new InvalidOperationException("An automatic value changed evaluation order: " + string.Join(",", source.Events));
            if (!threw && (result is null || result.Value.Number != (reuse ? 1 : 11) || ReferenceEquals(previous, result) != reuse))
                throw new InvalidOperationException("The constructor value was evaluated twice or overwritten.");
            if (previous is not null && (!reuse || threw) && previous.Value.Number != 50)
                throw new InvalidOperationException("The previous destination changed unexpectedly.");
        }

        public static void VerifyTuple(bool resolve, bool reference, string operation, bool throwMember)
        {
            var source = new Source { Reuse = operation == "Reuse", ThrowMember = throwMember };
            bool construct = operation == "Create" || resolve && operation == "Replace";
            bool skip = reference && !construct;
            int id = -1;
            string? name = null;
            bool threw = false;
            try
            {
                if (reference)
                {
                    ITypeMapper<Source, Tuple<int, string>> mapper = resolve ? new ResolveReferenceTupleMapper() : new ConstructReferenceTupleMapper();
                    var previous = Tuple.Create(40, "old");
                    var result = operation == "Create" ? mapper.Create(source) : mapper.Update(source, previous);
                    id = result.Item1; name = result.Item2;
                    if (ReferenceEquals(previous, result) == construct)
                        throw new InvalidOperationException("Reference tuple construction or reuse changed.");
                }
                else
                {
                    ITypeMapper<Source, (int Id, string Name)> mapper = resolve ? new ResolveValueTupleMapper() : new ConstructValueTupleMapper();
                    (int Id, string Name) result = operation == "Create" ? mapper.Create(source) : mapper.Update(source, (40, "old"));
                    id = result.Id; name = result.Name;
                }
            }
            catch (ProbeException) { threw = true; }
            string expected = construct ? "id,member" : skip ? "" : "member";
            if (threw != (throwMember && !skip) || string.Join(",", source.Events) != expected)
                throw new InvalidOperationException("Tuple element order or conditional evaluation changed: " + string.Join(",", source.Events));
            if (!threw && (id != (construct ? 1 : 40) || name != (construct ? "member:2" : skip ? "old" : "member:1")))
                throw new InvalidOperationException("Ignored tuple input or explicit member value changed.");
        }
    }
}
