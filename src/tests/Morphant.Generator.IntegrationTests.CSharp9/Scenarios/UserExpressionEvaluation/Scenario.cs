#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UserExpressionEvaluation
{
    public sealed class Source
    {
        private readonly Calls _mapper;

        public Source(bool preferred)
        {
            Preferred = preferred;
            _mapper = new Calls(Events);
        }

        public List<string> Events { get; } = new();
        public bool Preferred { get; }
        public Calls Mapper
        {
            get { Events.Add("receiver"); return _mapper; }
        }

        public int Read(string name, int value) { Events.Add(name); return value; }
        public bool Select() { Events.Add("condition"); return Preferred; }
    }

    public sealed class Calls
    {
        private readonly List<string> _events;
        public Calls(List<string> events) => _events = events;

        public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        {
            _events.Add("call");
            return destination;
        }
    }

    public sealed class Destination
    {
        public Destination(int first, int value) { First = first; InitialValue = value; Value = value; }
        public int First { get; }
        public int InitialValue { get; }
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class DirectMapper : TypeMapper<DirectMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(source.Read("first", 1),
                    source.Mapper.Map<int, int>(source.Read("constructor-source", 2), destination:
                        source.Select() ? source.Read("constructor-yes", 3) : source.Read("constructor-no", 4))))
                .Members(source => new()
                {
                    Value = source.Mapper.Map<int, int>(source.Read("member-source", 5), destination:
                        source.Select() ? source.Read("member-yes", 6) : source.Read("member-no", 7))
                });
    }

    [MorphantMapper]
    public partial class LocalMapper : TypeMapper<LocalMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source =>
                {
                    var first = source.Read("first", 1);
                    var value = source.Mapper.Map<int, int>(source.Read("constructor-source", 2), destination:
                        source.Select() ? source.Read("constructor-yes", 3) : source.Read("constructor-no", 4));
                    return new(first, value);
                })
                .Members(source =>
                {
                    var value = source.Mapper.Map<int, int>(source.Read("member-source", 5), destination:
                        source.Select() ? source.Read("member-yes", 6) : source.Read("member-no", 7));
                    return new() { Value = value };
                });
    }

    public static class Scenario
    {
        public static void Verify(bool locals, bool preferred)
        {
            ITypeMapper<Source, Destination> mapper = locals ? new LocalMapper() : new DirectMapper();
            var source = new Source(preferred);
            var constructorEvents = preferred
                ? "first,receiver,constructor-source,condition,constructor-yes,call,"
                : "first,receiver,constructor-source,condition,constructor-no,call,";
            var memberEvents = preferred
                ? "receiver,member-source,condition,member-yes,call"
                : "receiver,member-source,condition,member-no,call";

            var created = mapper.Create(source);
            if (created.First != 1 || created.InitialValue != (preferred ? 3 : 4) ||
                created.Value != (preferred ? 6 : 7) || string.Join(",", source.Events) != constructorEvents + memberEvents)
                throw new InvalidOperationException("Create changed user invocation order or evaluated the wrong branch.");

            source.Events.Clear();
            var replaced = mapper.Update(source, null);
            if (replaced.First != 1 || replaced.InitialValue != (preferred ? 3 : 4) ||
                replaced.Value != (preferred ? 6 : 7) || string.Join(",", source.Events) != constructorEvents + memberEvents)
                throw new InvalidOperationException("Replacement changed user invocation order or repeated an evaluation.");

            source.Events.Clear();
            var destination = new Destination(10, 20);
            var updated = mapper.Update(source, destination);
            if (!ReferenceEquals(updated, destination) || updated.First != 10 || updated.InitialValue != 20 ||
                updated.Value != (preferred ? 6 : 7) || string.Join(",", source.Events) != memberEvents)
                throw new InvalidOperationException("Update changed user invocation order or ran construction.");
        }
    }
}
