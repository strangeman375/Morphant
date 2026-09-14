#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;
using DestinationMembers = Morphant.Generated.N_0550d486ae47037d4905efca6830c0bf.DestinationMembers;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SharedNestedUpdate
{
    public enum MappingKind { Constructor, Factory, OperationGuard, PreviousGuard }

    public sealed class Source
    {
        public readonly List<string> Events = new();
        public bool HasChild { get; set; }
        public bool NullFactory { get; set; }
        public ChildDestination? CreatedChild { get; private set; }

        public ChildDestination? CreateChild()
        {
            Events.Add("argument");
            return CreatedChild = HasChild ? new ChildDestination(Events) : null;
        }

        public Destination CreateDestination()
        {
            Events.Add("factory");
            return NullFactory ? null! : new Destination(CreateChild(), Events);
        }

        public ChildSource ReadChild(MappingOperation operation)
        {
            Events.Add("source:" + operation);
            return new ChildSource(Events);
        }
    }

    public sealed class ChildSource
    {
        public ChildSource(List<string> events) => Events = events;
        public List<string> Events { get; }
        public string ReadName(MappingOperation operation)
        {
            Events.Add("nested:" + operation);
            return "updated";
        }
    }

    public sealed class ChildDestination
    {
        public ChildDestination(List<string> events) => Events = events;
        public List<string> Events { get; }
        public string Name { get; set; } = "initial";
    }

    public sealed class Destination
    {
        private readonly List<string> _events;
        internal readonly ChildDestination? OriginalChild;
        public int Observed { get; set; }

        public Destination(ChildDestination? child, List<string> events)
        {
            events.Add("construct");
            OriginalChild = child;
            _events = events;
        }

        public ChildDestination? Child
        {
            get
            {
                _events.Add("target");
                return OriginalChild;
            }
        }
    }

    [MorphantMapper]
    public partial class ConstructorMapper : TypeMapper<ConstructorMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>()
                .Members((source, previous, result, context) => new() { Name = source.ReadName(context.Operation) });
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.CreateChild(), source.Events))
                .Members((source, previous, result, context) =>
                {
                    var members = new DestinationMembers();
                    Update<ChildDestination>(source.ReadChild(context.Operation), members.Child);
                    return members;
                });
        }
    }

    [MorphantMapper]
    public partial class FactoryMapper : TypeMapper<FactoryMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>()
                .Members((source, previous, result, context) => new() { Name = source.ReadName(context.Operation) });
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .ConstructUsing(source => source.CreateDestination())
                .Members((source, previous, result, context) =>
                {
                    var members = new DestinationMembers();
                    Update<ChildDestination>(source.ReadChild(context.Operation), members.Child);
                    return members;
                });
        }
    }

    [MorphantMapper]
    public partial class OperationGuardMapper : TypeMapper<OperationGuardMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>()
                .Members((source, previous, result, context) => new() { Name = source.ReadName(context.Operation) });
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.CreateChild(), source.Events))
                .Members((source, previous, result, context) =>
                {
                    var members = new DestinationMembers();
                    if (context.Operation == MappingOperation.Update)
                        Update<ChildDestination>(source.ReadChild(context.Operation), members.Child);
                    return members;
                });
        }
    }

    [MorphantMapper]
    public partial class PreviousGuardMapper : TypeMapper<PreviousGuardMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>()
                .Members((source, previous, result, context) => new() { Name = source.ReadName(context.Operation) });
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.CreateChild(), source.Events))
                .Members((source, previous, result, context) =>
                {
                    var members = new DestinationMembers();
                    if (previous.HasValue)
                        Update<ChildDestination>(source.ReadChild(context.Operation), members.Child);
                    return members;
                });
        }
    }

    public static class Scenario
    {
        public static void Verify(MappingKind kind, string operation, bool hasChild)
        {
            var source = new Source { HasChild = hasChild };
            ITypeMapper<Source, Destination> mapper = kind switch
            {
                MappingKind.Constructor => new ConstructorMapper(),
                MappingKind.Factory => new FactoryMapper(),
                MappingKind.OperationGuard => new OperationGuardMapper(),
                _ => new PreviousGuardMapper()
            };
            var previous = operation == "UpdateExisting"
                ? new Destination(hasChild ? new ChildDestination(source.Events) : null, source.Events) : null;
            source.Events.Clear();
            var result = operation == "Create" ? mapper.Create(source) : mapper.Update(source, previous);
            var expected = new List<string>();
            if (previous is null)
            {
                if (kind == MappingKind.Factory) expected.Add("factory");
                expected.AddRange(new[] { "argument", "construct" });
            }
            var runs = kind switch
            {
                MappingKind.OperationGuard => operation != "Create",
                MappingKind.PreviousGuard => previous is not null,
                _ => true
            };
            if (runs)
            {
                expected.Add("target");
                if (hasChild)
                    expected.AddRange(new[] { "source:" + (operation == "Create" ? "Create" : "Update"), "nested:Update" });
            }
            if (string.Join(",", source.Events) != string.Join(",", expected))
                throw new InvalidOperationException("Wrong evaluation order: " + string.Join(",", source.Events));
            if (previous is not null && !ReferenceEquals(result, previous) ||
                !ReferenceEquals(result.OriginalChild, previous is null ? source.CreatedChild : previous.OriginalChild) ||
                hasChild && result.OriginalChild!.Name != (runs ? "updated" : "initial"))
                throw new InvalidOperationException("The outer or child destination was replaced or updated incorrectly.");
        }

        public static void VerifyNullFactory(bool update)
        {
            var source = new Source { NullFactory = true };
            ITypeMapper<Source, Destination> mapper = new FactoryMapper();
            var result = update ? mapper.Update(source, null) : mapper.Create(source);
            if (result is not null || string.Join(",", source.Events) != "factory")
                throw new InvalidOperationException("A null factory result must skip all nested work.");
        }

        public static void VerifyMutableState(string operation, bool local)
        {
            var events = new List<string>();
            var previous = operation == "UpdateExisting" ? new Destination(new ChildDestination(events), events) : null;
            Destination result;
            if (local)
            {
                ITypeMapper<Source, Destination> mapper = new MutableLocalMapper();
                var source = new Source { HasChild = true };
                result = operation == "Create" ? mapper.Create(source) : mapper.Update(source, previous);
            }
            else
            {
                ITypeMapper<MutableSource, Destination> mapper = new MutableSourceMapper();
                var source = new MutableSource { Events = events };
                result = operation == "Create" ? mapper.Create(source) : mapper.Update(source, previous);
            }
            if (result.Observed != 2 || result.OriginalChild?.Name != "updated")
                throw new InvalidOperationException("Nested source mutations were copied or lost.");
        }

        public static void VerifyContextAccess(bool capture, bool hasChild)
        {
            var events = new List<string>();
            var destination = hasChild ? new ChildDestination(events) : null;
            try
            {
                if (capture)
                    Runtime.MappingHelpers.UpdateExisting(destination, () => Read(), default);
                else
                    Runtime.MappingHelpers.UpdateExisting(destination, events,
                        static state => { state.Add("read"); return new ChildSource(state); }, default);
                if (hasChild) throw new InvalidOperationException("The invalid context was not accessed.");
            }
            catch (Exceptions.InvalidMappingContextException) when (hasChild) { }
            if (string.Join(",", events) != (hasChild ? "read" : ""))
                throw new InvalidOperationException("Source evaluation must follow the null guard and precede context access.");

            ChildSource Read() { events.Add("read"); return new ChildSource(events); }
        }
    }

    public struct MutableSource
    {
        public List<string> Events;
        public int Calls;
        public ChildSource ReadChild() { Calls++; return new ChildSource(Events); }
    }

    [MorphantMapper]
    public partial class MutableSourceMapper : TypeMapper<MutableSourceMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>()
                .Members((source, previous, result, context) => new() { Name = source.ReadName(context.Operation) });
            builder.Map<MutableSource, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(new ChildDestination(source.Events), source.Events))
                .Members((source, _) =>
                {
                    var members = new DestinationMembers();
                    Update<ChildDestination>(source.ReadChild(), members.Child);
                    Update<ChildDestination>(source.ReadChild(), members.Child);
                    return members with { Observed = source.Calls };
                });
        }
    }

    [MorphantMapper]
    public partial class MutableLocalMapper : TypeMapper<MutableLocalMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>()
                .Members((source, previous, result, context) => new() { Name = source.ReadName(context.Operation) });
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.CreateChild(), source.Events))
                .Members((source, _) =>
                {
                    var calls = 0;
                    var members = new DestinationMembers();
                    Update<ChildDestination>(ReadChild(source, ref calls), members.Child);
                    Update<ChildDestination>(ReadChild(source, ref calls), members.Child);
                    return members with { Observed = calls };
                });
        }

        private static ChildSource ReadChild(Source source, ref int calls)
        {
            calls++;
            return source.ReadChild(MappingOperation.Update);
        }
    }
}
