#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.PreparedNestedDestination
{
    public sealed class ChildDestination { public string Name { get; set; } = "initial"; }
    public sealed class ChildSource
    {
        public ChildSource(Source owner) => Owner = owner;
        public Source Owner { get; }
    }

    public sealed class Source
    {
        public List<string> Events { get; } = new();
        public bool NullChild { get; set; }
        public bool ReplaceChild { get; set; }
        public bool ExplicitCreate { get; set; }
        public bool InitFirst { get; set; }
        public ChildDestination? Prepared { get; private set; }
        public ChildDestination? Supplied { get; private set; }
        public MappingOperation NestedOperation { get; private set; }

        public ChildDestination Prepare()
        {
            Events.Add("prepare");
            Prepared = NullChild ? null : new ChildDestination();
            return Prepared!;
        }

        public ChildSource Read(MappingOperation operation, bool hasPrevious)
        {
            Events.Add("source:" + operation + ":" + hasPrevious);
            return new ChildSource(this);
        }

        public int ReadId() { Events.Add("initializer"); return 7; }

        public ChildDestination Apply(Option<ChildDestination> previous, MappingContext context)
        {
            Events.Add("map");
            NestedOperation = context.Operation;
            Supplied = previous.HasValue ? previous.Value : null;
            var child = ReplaceChild || Supplied is null ? new ChildDestination() : Supplied;
            child.Name = "mapped";
            return child;
        }
    }

    public class Destination
    {
        private ChildDestination _child;
        private readonly List<string> _events;
        public Destination(ChildDestination child, List<string> events)
        {
            _child = child;
            _events = events;
            events.Add("construct");
        }
        public ChildDestination Child
        {
            get { _events.Add("get"); return _child; }
            set { _events.Add("set"); _child = value; }
        }
    }
    public sealed class FactoryDestination : Destination
    {
        public FactoryDestination(ChildDestination child, List<string> events) : base(child, events) { }
    }
    public sealed class ResolvedDestination : Destination
    {
        public ResolvedDestination(ChildDestination child, List<string> events) : base(child, events) { }
    }
    public sealed class InitDestination : Destination
    {
        public InitDestination(ChildDestination child, List<string> events) : base(child, events) { }
        public int Id { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>()
                .Convert((source, previous, context) => source!.Owner.Apply(previous, context));
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.Prepare(), source.Events))
                .Members((source, previous, result, context) => new()
                {
                    Child = Map<ChildDestination>(source.Read(context.Operation, previous.HasValue))
                });
            builder.Map<Source, FactoryDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .ConstructUsing(source => new FactoryDestination(source.Prepare(), source.Events))
                .Members((source, previous, result, context) => new()
                {
                    Child = Map<ChildDestination>(source.Read(context.Operation, previous.HasValue))
                });
            builder.Map<Source, ResolvedDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .ResolveUsing((source, previous) => new ResolvedDestination(source.Prepare(), source.Events))
                .Members((source, previous, result, context) => new()
                {
                    Child = Map<ChildDestination>(source.Read(context.Operation, previous.HasValue))
                });
            builder.Map<Source, Tuple<ChildDestination, int>>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.Prepare(), 7))
                .Members((source, previous, result, context) => new()
                {
                    Item1 = Map<ChildDestination>(source.Read(context.Operation, previous.HasValue))
                });
            builder.Map<Source, (ChildDestination Child, int Id)>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.Prepare(), 7))
                .Members((source, previous, result, context) => new()
                {
                    Child = Map<ChildDestination>(source.Read(context.Operation, previous.HasValue))
                });
            builder.Map<Source, Tuple<ChildDestination, int, int>>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.Prepare(), 7, 9))
                .Members((source, previous, result, context) =>
                {
                    if (source.ExplicitCreate)
                        return new() { Item1 = Create<ChildDestination>(source.Read(context.Operation, previous.HasValue)) };
                    return new() { Item1 = Map<ChildDestination>(source.Read(context.Operation, previous.HasValue)) };
                });
            builder.Map<Source, InitDestination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.Prepare(), source.Events))
                .Members((source, previous, result, context) =>
                {
                    if (source.InitFirst)
                        return new()
                        {
                            Id = source.ReadId(),
                            Child = Map<ChildDestination>(source.Read(context.Operation, previous.HasValue))
                        };
                    return new()
                    {
                        Child = Map<ChildDestination>(source.Read(context.Operation, previous.HasValue)),
                        Id = source.ReadId()
                    };
                });
        }
    }

    public static class Scenario
    {
        public static void Verify(string kind, bool updateNull, bool nullChild, bool replace, bool explicitCreate = false)
        {
            var source = new Source { NullChild = nullChild, ReplaceChild = replace,
                ExplicitCreate = explicitCreate, InitFirst = kind == "InitBefore" };
            var mapper = new TestMapper();
            ChildDestination child;
            var objectDestination = kind is "Object" or "Factory" or "ResolveFactory";
            switch (kind)
            {
                case "Object":
                    child = Run((ITypeMapper<Source, Destination>)mapper, source, updateNull).Child;
                    break;
                case "Factory":
                    child = Run((ITypeMapper<Source, FactoryDestination>)mapper, source, updateNull).Child;
                    break;
                case "ResolveFactory":
                    child = Run((ITypeMapper<Source, ResolvedDestination>)mapper, source, updateNull).Child;
                    break;
                case "SystemTuple":
                    var tuple = Run((ITypeMapper<Source, Tuple<ChildDestination, int>>)mapper, source, updateNull);
                    child = tuple.Item1;
                    if (tuple.Item2 != 7) throw new InvalidOperationException("The other tuple element changed.");
                    break;
                case "ValueTuple":
                    var value = ((ITypeMapper<Source, (ChildDestination Child, int Id)>)mapper).Create(source);
                    child = value.Child;
                    if (value.Id != 7) throw new InvalidOperationException("The other tuple element changed.");
                    break;
                case "InitBefore":
                case "InitAfter":
                    var initialized = Run((ITypeMapper<Source, InitDestination>)mapper, source, updateNull);
                    child = initialized.Child;
                    if (initialized.Id != 7) throw new InvalidOperationException("The init-only value changed.");
                    break;
                default:
                    var conditional = Run((ITypeMapper<Source, Tuple<ChildDestination, int, int>>)mapper, source, updateNull);
                    child = conditional.Item1;
                    if (conditional.Item2 != 7 || conditional.Item3 != 9)
                        throw new InvalidOperationException("The other tuple elements changed.");
                    break;
            }

            var outer = updateNull ? MappingOperation.Update : MappingOperation.Create;
            var expectedEvents = objectDestination
                ? "prepare,construct,source:" + outer + ":False,get,map,set,get"
                : "prepare,source:" + outer + ":False,map";
            if (kind == "InitBefore")
                expectedEvents = "prepare,construct,initializer,source:" + outer + ":False,get,map,set,get";
            if (kind == "InitAfter")
                expectedEvents = "prepare,construct,source:" + outer + ":False,map,set,initializer,get";
            var createsChild = explicitCreate || kind == "InitAfter";
            if (string.Join(",", source.Events) != expectedEvents)
                throw new InvalidOperationException("Unexpected evaluation order: " + string.Join(",", source.Events));
            if (source.NestedOperation != (createsChild ? MappingOperation.Create : MappingOperation.Update) ||
                !ReferenceEquals(source.Supplied, createsChild ? null : source.Prepared) || child.Name != "mapped")
                throw new InvalidOperationException("The nested operation lost its prepared destination or result.");
            var shouldReuse = !nullChild && !replace && !createsChild;
            if (ReferenceEquals(child, source.Prepared) != shouldReuse)
                throw new InvalidOperationException("The returned nested destination was not retained.");
        }

        private static T Run<T>(ITypeMapper<Source, T> mapper, Source source, bool updateNull) where T : class =>
            updateNull ? mapper.Update(source, null) : mapper.Create(source);
    }
}
