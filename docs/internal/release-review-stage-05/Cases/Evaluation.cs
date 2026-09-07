using System;
using System.Collections.Generic;
using System.Linq;
using Morphant;
using Stage05Audit.Cases.Extensions;

namespace Stage05Audit.Cases.Extensions
{
    public static class NodeExtensions
    {
        public static int Read(this Node value) => Trace.Value("extension", value.Value);
    }
}

namespace Stage05Audit.Cases
{
    public sealed class Node { public int Value { get; set; } }
    public sealed class Source
    {
        public int Value { get; set; }
        public bool Select { get; set; }
        public Node? Node { get; set; }
        public Node? ReadNode() { Trace.Events.Add("receiver"); return Node; }
    }
    public sealed class Destination
    {
        public int A { get; set; }
        public int B { get; set; }
    }
    public sealed class InitDestination
    {
        public int Created { get; init; }
        public int Updated { get; set; }
    }
    public sealed class LazyDestination
    {
        public int Value { get; set; }
        public Func<int> Later { get; set; } = () => 0;
    }
    public sealed class ConditionalDestination
    {
        public int Value { get; set; }
    }
    public sealed class ExceptionDestination
    {
        public ExceptionDestination(int value) { Value = value; }
        public int Value { get; }
    }

    public static class Trace
    {
        public static readonly List<string> Events = new();
        public static int Value(string name, int value) { Events.Add(name); return value; }
        public static bool Test(bool value) { Events.Add("condition"); return value; }
        public static int Fail() => throw new InvalidOperationException("selected");
    }

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>().Members(source =>
            {
                var shared = Trace.Value("shared", source.Value);
                if (Trace.Test(source.Select))
                    return new() { A = shared, B = shared + Trace.Value("true", 1) };
                return new() { A = shared, B = shared + Trace.Value("false", 2) };
            });
            builder.Map<Source, InitDestination>().Members(source => new()
            {
                Created = Trace.Value("init", source.Value),
                Updated = Trace.Value("set", source.Value + 1)
            });
            builder.Map<Source, LazyDestination>().Members(source =>
            {
                var unused = Trace.Fail();
                return new()
                {
                    Value = source.Value,
                    Later = Value<Func<int>>(() => Trace.Value("deferred", source.Value))
                };
            });
            builder.Map<Source, ConditionalDestination>().Members(source => new()
            {
                Value = source.ReadNode()?.Read() ?? Trace.Value("fallback", -1)
            });
            builder.Map<Source, ExceptionDestination>().Construct(source =>
                source.Select ? new(source.Value) : throw new InvalidOperationException("selected"));
        }
    }

    public static class Scenario
    {
        public static void Run()
        {
            var mapper = new Mapper();
            var source = new Source { Value = 10, Select = true, Node = new Node { Value = 8 } };
            var members = (ITypeMapper<Source, Destination>)mapper;
            Trace.Events.Clear();
            var selected = members.Create(source);
            Check.Equal("selected true value", 11, selected.B);
            Check.Equal("shared evaluated once", 1, Trace.Events.Count(x => x == "shared"));
            Check.Equal("condition evaluated once", 1, Trace.Events.Count(x => x == "condition"));
            Check.Equal("unselected false skipped", false, Trace.Events.Contains("false"));
            source.Select = false;
            Trace.Events.Clear();
            var updated = members.Update(source, selected);
            Check.Equal("updated instance", true, ReferenceEquals(selected, updated));
            Check.Equal("selected false value", 12, updated.B);
            Check.Equal("update shared once", 1, Trace.Events.Count(x => x == "shared"));
            Check.Equal("unselected true skipped", false, Trace.Events.Contains("true"));

            var initialization = (ITypeMapper<Source, InitDestination>)mapper;
            Trace.Events.Clear();
            var initialized = initialization.Create(source);
            Check.Equal("created init value", 10, initialized.Created);
            Check.Equal("created init evaluated once", 1, Trace.Events.Count(x => x == "init"));
            source.Value = 20;
            Trace.Events.Clear();
            initialized = initialization.Update(source, initialized);
            Check.Equal("update preserves init", 10, initialized.Created);
            Check.Equal("update set value", 21, initialized.Updated);
            Check.Sequence("inapplicable init skipped", "set", Trace.Events);

            Trace.Events.Clear();
            var lazy = ((ITypeMapper<Source, LazyDestination>)mapper).Create(source);
            Check.Equal("unused throwing local skipped", 20, lazy.Value);
            Check.Sequence("delegate not invoked eagerly", "", Trace.Events);
            source.Value = 30;
            Check.Equal("deferred source capture", 30, lazy.Later());
            Check.Sequence("delegate invoked once", "deferred", Trace.Events);

            var conditional = (ITypeMapper<Source, ConditionalDestination>)mapper;
            Trace.Events.Clear();
            Check.Equal("conditional extension value", 8, conditional.Create(source).Value);
            Check.Sequence("receiver and extension once", "receiver,extension", Trace.Events);
            source.Node = null;
            Trace.Events.Clear();
            Check.Equal("conditional fallback value", -1, conditional.Create(source).Value);
            Check.Sequence("null skips extension", "receiver,fallback", Trace.Events);

            var exceptional = (ITypeMapper<Source, ExceptionDestination>)mapper;
            Check.Throws<InvalidOperationException>("selected throw", () => exceptional.Create(source));
            source.Select = true;
            Check.Equal("unselected throw skipped", 30, exceptional.Create(source).Value);
        }
    }
}
