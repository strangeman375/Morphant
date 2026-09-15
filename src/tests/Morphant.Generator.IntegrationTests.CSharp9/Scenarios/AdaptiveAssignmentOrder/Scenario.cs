#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.AdaptiveAssignmentOrder
{
    public sealed record Child(int Value);

    public sealed class Source
    {
        public readonly List<string> Events = new List<string>();
        public bool Reuse { get; set; }
        public string Mode { get; set; } = "Compatible";
        public bool ThrowSource { get; set; }
        public int Number { get; set; } = 3;
        public Child ReadChild()
        {
            Events.Add("source child");
            if (ThrowSource && Events.Contains("set child")) throw new ProbeException();
            return new Child(Number);
        }
    }
    public sealed class ProbeException : Exception { }

    public sealed class Destination
    {
        private readonly Source _source;
        private Child _child;
        private object? _reference;
        private object _value = 2;
        public Destination(Source source)
        {
            _source = source;
            _child = new Child(40);
            _reference = new Child(10);
            source.Events.Add("construct");
        }
        public Child Child
        {
            get { _source.Events.Add("read child"); return _child; }
            set
            {
                _source.Events.Add("set child");
                _child = value;
                _source.Number = 100;
                _reference = _source.Mode == "Null" ? null
                    : _source.Mode == "Incompatible" ? (object)"wrong" : new Child(value.Value);
            }
        }
        public object? Reference
        {
            get { _source.Events.Add("read reference"); return _reference; }
            set { _source.Events.Add("set reference"); _reference = value; }
        }
        public object Value
        {
            get { _source.Events.Add("read value"); return _value; }
            set { _source.Events.Add("set value"); _value = value; }
        }
        public int Tail { get; set; } = -1;
        public int Observed { get; set; } = -1;
        public int ChildValue => _child.Value;
        public object? ReferenceValue => _reference;
        public int NumberValue => (int)_value;
    }

    [MorphantMapper]
    public partial class ConstructMapper : TypeMapper<ConstructMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Child, Child>().Convert((source, previous) =>
                new Child(source!.Value + (previous.HasValue ? previous.Value.Value : 0)));
            builder.Map<int, int>().Convert((source, previous) => source + (previous.HasValue ? previous.Value : 0));
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source))
                .Members((source, _) => new()
                {
                    Child = Map(source.ReadChild()),
                    Reference = Map<Child>(source.ReadChild()),
                    Value = Map<int>(source.Number),
                    Tail = source.Number,
                    Observed = source.Number
                });
        }
    }

    [MorphantMapper]
    public partial class ConstructResultMapper : TypeMapper<ConstructResultMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Child, Child>().Convert((source, previous) =>
                new Child(source!.Value + (previous.HasValue ? previous.Value.Value : 0)));
            builder.Map<int, int>().Convert((source, previous) => source + (previous.HasValue ? previous.Value : 0));
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source))
                .Members((source, _, result) => new()
                {
                    Child = Map(source.ReadChild()),
                    Reference = Map<Child>(source.ReadChild()),
                    Value = Map<int>(source.Number),
                    Tail = source.Number,
                    Observed = result.Child.Value
                });
        }
    }

    [MorphantMapper]
    public partial class ResolveMapper : TypeMapper<ResolveMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Child, Child>().Convert((source, previous) =>
                new Child(source!.Value + (previous.HasValue ? previous.Value.Value : 0)));
            builder.Map<int, int>().Convert((source, previous) => source + (previous.HasValue ? previous.Value : 0));
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Resolve((source, previous) =>
                {
                    if (previous.TryGetValue(out var existing) && source.Reuse) return existing;
                    return new(source);
                })
                .Members((source, _) => new()
                {
                    Child = Map(source.ReadChild()),
                    Reference = Map<Child>(source.ReadChild()),
                    Value = Map<int>(source.Number),
                    Tail = source.Number,
                    Observed = source.Number
                });
        }
    }

    [MorphantMapper]
    public partial class ResolveResultMapper : TypeMapper<ResolveResultMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Child, Child>().Convert((source, previous) =>
                new Child(source!.Value + (previous.HasValue ? previous.Value.Value : 0)));
            builder.Map<int, int>().Convert((source, previous) => source + (previous.HasValue ? previous.Value : 0));
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Resolve((source, previous) =>
                {
                    if (previous.TryGetValue(out var existing) && source.Reuse) return existing;
                    return new(source);
                })
                .Members((source, _, result) => new()
                {
                    Child = Map(source.ReadChild()),
                    Reference = Map<Child>(source.ReadChild()),
                    Value = Map<int>(source.Number),
                    Tail = source.Number,
                    Observed = result.Child.Value
                });
        }
    }

    [MorphantMapper]
    public partial class ConstructUsingMapper : TypeMapper<ConstructUsingMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Child, Child>().Convert((source, previous) =>
                new Child(source!.Value + (previous.HasValue ? previous.Value.Value : 0)));
            builder.Map<int, int>().Convert((source, previous) => source + (previous.HasValue ? previous.Value : 0));
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .ConstructUsing(source => new Destination(source))
                .Members((source, _) => new()
                {
                    Child = Map(source.ReadChild()),
                    Reference = Map<Child>(source.ReadChild()),
                    Value = Map<int>(source.Number),
                    Tail = source.Number,
                    Observed = source.Number
                });
        }
    }

    [MorphantMapper]
    public partial class ConstructUsingResultMapper : TypeMapper<ConstructUsingResultMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Child, Child>().Convert((source, previous) =>
                new Child(source!.Value + (previous.HasValue ? previous.Value.Value : 0)));
            builder.Map<int, int>().Convert((source, previous) => source + (previous.HasValue ? previous.Value : 0));
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .ConstructUsing(source => new Destination(source))
                .Members((source, _, result) => new()
                {
                    Child = Map(source.ReadChild()),
                    Reference = Map<Child>(source.ReadChild()),
                    Value = Map<int>(source.Number),
                    Tail = source.Number,
                    Observed = result.Child.Value
                });
        }
    }

    [MorphantMapper]
    public partial class ResolveUsingMapper : TypeMapper<ResolveUsingMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Child, Child>().Convert((source, previous) =>
                new Child(source!.Value + (previous.HasValue ? previous.Value.Value : 0)));
            builder.Map<int, int>().Convert((source, previous) => source + (previous.HasValue ? previous.Value : 0));
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .ResolveUsing((source, previous) => previous.HasValue && source.Reuse
                    ? previous.Value : new Destination(source))
                .Members((source, _) => new()
                {
                    Child = Map(source.ReadChild()),
                    Reference = Map<Child>(source.ReadChild()),
                    Value = Map<int>(source.Number),
                    Tail = source.Number,
                    Observed = source.Number
                });
        }
    }

    [MorphantMapper]
    public partial class ResolveUsingResultMapper : TypeMapper<ResolveUsingResultMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Child, Child>().Convert((source, previous) =>
                new Child(source!.Value + (previous.HasValue ? previous.Value.Value : 0)));
            builder.Map<int, int>().Convert((source, previous) => source + (previous.HasValue ? previous.Value : 0));
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .ResolveUsing((source, previous) => previous.HasValue && source.Reuse
                    ? previous.Value : new Destination(source))
                .Members((source, _, result) => new()
                {
                    Child = Map(source.ReadChild()),
                    Reference = Map<Child>(source.ReadChild()),
                    Value = Map<int>(source.Number),
                    Tail = source.Number,
                    Observed = result.Child.Value
                });
        }
    }

    public static class Scenario
    {
        public static void Verify(string form, string operation, string mode, bool readsResult, bool throwSource)
        {
            ITypeMapper<Source, Destination> mapper = (form, readsResult) switch
            {
                ("Construct", false) => new ConstructMapper(),
                ("Construct", true) => new ConstructResultMapper(),
                ("Resolve", false) => new ResolveMapper(),
                ("Resolve", true) => new ResolveResultMapper(),
                ("ConstructUsing", false) => new ConstructUsingMapper(),
                ("ConstructUsing", true) => new ConstructUsingResultMapper(),
                ("ResolveUsing", false) => new ResolveUsingMapper(),
                ("ResolveUsing", true) => new ResolveUsingResultMapper(),
                _ => throw new ArgumentException(nameof(form))
            };
            var source = new Source { Reuse = operation == "Reuse", Mode = mode, ThrowSource = throwSource };
            var previous = operation == "Reuse" || operation == "Replace" ? new Destination(source) : null;
            source.Events.Clear();
            bool reuse = previous is not null && (!form.StartsWith("Resolve", StringComparison.Ordinal) || source.Reuse);
            Destination? result = null;
            string? failure = null;
            try
            {
                result = operation == "Create" ? mapper.Create(source) : mapper.Update(source, previous);
            }
            catch (NestedDestinationTypeMismatchException) { failure = "Mismatch"; }
            catch (ProbeException) { failure = "Source"; }
            string prefix = reuse ? "" : "construct,";
            string expected = prefix + "source child,read child,";
            string? expectedFailure = !readsResult && throwSource ? "Source"
                : !readsResult && mode == "Incompatible" ? "Mismatch" : null;
            if (!readsResult)
            {
                expected += "set child,source child";
                if (expectedFailure != "Source") expected += ",read reference";
                if (expectedFailure is null) expected += ",set reference,read value,set value";
            }
            else expected += "source child,read reference,read value,read child,set child,set reference,set value";
            if (string.Join(",", source.Events) != expected || failure != expectedFailure)
                throw new InvalidOperationException("Unexpected mapping order: " + string.Join(",", source.Events)
                    + "; failure: " + failure + "; expected: " + expected + "; failure: " + expectedFailure);
            if (failure is null)
            {
                int reference = readsResult ? 13 : mode == "Null" ? 100 : 143;
                if (result is null || result.ChildValue != 43 || result.ReferenceValue is not Child child
                    || child.Value != reference || result.NumberValue != (readsResult ? 5 : 102)
                    || result.Tail != (readsResult ? 3 : 100) || result.Observed != (readsResult ? 40 : 100)
                    || ReferenceEquals(previous, result) != reuse)
                    throw new InvalidOperationException("A mapping used a stale member or source value.");
            }
            if (previous is not null && !reuse && (previous.ChildValue != 40 || previous.Tail != -1))
                throw new InvalidOperationException("Replacement mutated the previous destination.");
            if (previous is not null && reuse && failure is not null && (previous.ChildValue != 43 || previous.Tail != -1))
                throw new InvalidOperationException("Assignments around a failing expression changed.");
        }
    }
}
