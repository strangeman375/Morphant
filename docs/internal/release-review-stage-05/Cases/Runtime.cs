using System;
using System.Collections.Generic;
using Morphant;

namespace Stage05Audit.Cases
{
    public sealed class Source
    {
        public int[] Values { get; set; } = Array.Empty<int>();
        public bool Fail { get; set; }
    }
    public sealed class Destination { public int Value { get; set; } }
    public sealed class MethodDestination { public int Value { get; set; } }
    public sealed class DelegateDestination { public int Value { get; set; } }
    public sealed class AnonymousDestination { public int Value { get; set; } }
    public sealed class FactoryDestination { public int Value { get; set; } }
    public sealed class ResolverDestination { public int Value { get; set; } }

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        public List<string> Events { get; } = new();
        public int DelegateReads { get; private set; }
        public int Offset { get; set; } = 5;
        public Func<Source, DelegateDestination> Callback
        {
            get
            {
                DelegateReads++;
                return input => new DelegateDestination { Value = input.Values.Length + Offset };
            }
        }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination>().Convert((source, previous) =>
            {
                Events.Add("start");
                try
                {
                    if (source is null) return new Destination { Value = -1 };
                    var result = previous.HasValue ? previous.Value : new Destination();
                    foreach (int value in source.Values)
                    {
                        Events.Add("value:" + value);
                        result.Value += value;
                    }
                    if (source.Fail) throw new InvalidOperationException("manual");
                    return result;
                }
                finally { Events.Add("finally"); }
            });
            builder.Map<Source, MethodDestination>().Convert(Build);
            builder.Map<Source, DelegateDestination>().Convert(Callback);
            builder.Map<Source, AnonymousDestination>().Convert(delegate(Source input)
            {
                int Count() => input.Values.Length + Offset;
                return new AnonymousDestination { Value = Count() };
            });
            builder.Map<Source, FactoryDestination>().ConstructUsing(source =>
            {
                Events.Add("factory");
                var result = new FactoryDestination();
                foreach (int value in source.Values) result.Value += value;
                return result;
            });
            builder.Map<Source, ResolverDestination>().ResolveUsing((source, previous) =>
            {
                Events.Add("resolver");
                var result = previous.HasValue ? previous.Value : new ResolverDestination();
                for (int index = 0; index < source.Values.Length; index++)
                    result.Value += source.Values[index];
                return result;
            });
        }

        private MethodDestination Build(Source source) => new() { Value = source.Values.Length + Offset };
        private MethodDestination Build(object source) => new() { Value = -100 };
    }

    public static class Scenario
    {
        public static void Run()
        {
            var mapper = new Mapper();
            var source = new Source { Values = new[] { 2, 3 } };
            var conversion = (ITypeMapper<Source, Destination>)mapper;
            var created = conversion.Create(source);
            Check.Equal("Convert loop", 5, created.Value);
            Check.Sequence("Convert statement order", "start,value:2,value:3,finally", mapper.Events);
            mapper.Events.Clear();
            var updated = conversion.Update(source, created);
            Check.Equal("Convert mutation", 10, updated.Value);
            Check.Equal("Convert reuse", true, ReferenceEquals(created, updated));
            Check.Sequence("Update statement order", "start,value:2,value:3,finally", mapper.Events);
            source.Fail = true;
            mapper.Events.Clear();
            Check.Throws<InvalidOperationException>("Convert exception", () => conversion.Create(source));
            Check.Sequence("finally after throw", "start,value:2,value:3,finally", mapper.Events);
            mapper.Events.Clear();
            Check.Equal("Convert receives null", -1, conversion.Create(null!).Value);
            Check.Sequence("finally after null return", "start,finally", mapper.Events);

            Check.Equal("bound method overload", 7,
                ((ITypeMapper<Source, MethodDestination>)mapper).Create(source).Value);
            var callbacks = (ITypeMapper<Source, DelegateDestination>)mapper;
            Check.Equal("delegate member result", 7, callbacks.Create(source).Value);
            Check.Equal("delegate getter once", 1, mapper.DelegateReads);
            mapper.Offset = 10;
            Check.Equal("delegate observes mapper state", 12, callbacks.Create(source).Value);
            Check.Equal("delegate getter once per call", 2, mapper.DelegateReads);
            Check.Equal("anonymous delegate and local function", 12,
                ((ITypeMapper<Source, AnonymousDestination>)mapper).Create(source).Value);

            var factories = (ITypeMapper<Source, FactoryDestination>)mapper;
            mapper.Events.Clear();
            var factory = factories.Create(source);
            Check.Equal("ConstructUsing loop", 5, factory.Value);
            Check.Sequence("ConstructUsing called once", "factory", mapper.Events);
            mapper.Events.Clear();
            Check.Equal("ConstructUsing reuses existing", true,
                ReferenceEquals(factory, factories.Update(source, factory)));
            Check.Sequence("ConstructUsing skipped for existing", "", mapper.Events);

            var resolvers = (ITypeMapper<Source, ResolverDestination>)mapper;
            mapper.Events.Clear();
            var resolved = resolvers.Create(source);
            Check.Equal("ResolveUsing loop", 5, resolved.Value);
            resolved = resolvers.Update(source, resolved);
            Check.Equal("ResolveUsing update mutation", 10, resolved.Value);
            Check.Sequence("ResolveUsing called once per operation", "resolver,resolver", mapper.Events);
        }
    }
}
