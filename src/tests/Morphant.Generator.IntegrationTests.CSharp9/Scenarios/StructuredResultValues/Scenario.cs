#nullable enable
using System;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.StructuredResultValues
{
    public sealed class Source
    {
        public int Id { get; set; }
        public int Updated { get; set; }
        public bool Reuse { get; set; }
        public int Observations { get; private set; }
        public int LastReadId { get; private set; }
        public string LastReadText { get; private set; } = "";
        public bool Observe(int id, string text)
        {
            Observations++;
            LastReadId = id;
            LastReadText = text;
            return true;
        }
    }

    public struct Destination
    {
        public static int Constructions { get; set; }
        public Destination(int id)
        {
            Id = id;
            Updated = 0;
            Constructions++;
        }
        public int Id { get; }
        public int Updated { get; set; }
        public readonly string Text => Id.ToString();
    }

    public static class DestinationExtensions
    {
        public static int ReadCopy(this Destination value)
        {
            value.Updated = 999;
            return value.Id;
        }
    }

    [MorphantMapper]
    public sealed partial class ValueMapper : TypeMapper<ValueMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Resolve((source, previous) =>
            {
                if (previous.HasValue && source.Reuse)
                {
                    var first = previous.Value;
                    var second = first;
                    var id = second.ReadCopy();
                    var text = second.Text.Trim();
                    var observed = source.Observe(id, text);
                    return second;
                }
                return new(source.Id);
            }).Members(source => new() { Updated = source.Updated });
    }

    [MorphantMapper]
    public sealed partial class NullableMapper : TypeMapper<NullableMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination?>().Resolve((source, previous) =>
            {
                if (previous.TryGetValue(out var selected) && source.Reuse)
                {
                    var alias = selected;
                    return alias;
                }
                return new(source.Id);
            }).Members(source => new() { Updated = source.Updated });
    }

    public sealed class ReferenceDestination
    {
        public static int Constructions { get; set; }
        public ReferenceDestination(int id) { Id = id; Constructions++; }
        public int Id { get; }
        public int Updated { get; set; }
    }

#pragma warning disable MORPH0062
    [MorphantMapper]
    public sealed partial class InvalidResolveMapper : TypeMapper<InvalidResolveMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, ReferenceDestination>().Resolve((source, previous) =>
            {
                if (previous.HasValue) return previous.Value;
                return new ReferenceDestination(source.Id);
            });
    }

    [MorphantMapper]
    public sealed partial class InvalidConstructMapper : TypeMapper<InvalidConstructMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, ReferenceDestination>()
                .Construct(source => new ReferenceDestination(source.Id));
    }
#pragma warning restore MORPH0062

#pragma warning disable MORPH0038
    [MorphantMapper]
    public sealed partial class InvalidReadMapper : TypeMapper<InvalidReadMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, ReferenceDestination>()
                .Resolve((source, previous) => new(previous.Value.Id));
    }
#pragma warning restore MORPH0038

    public static class Scenario
    {
        public static void VerifyValueCopies()
        {
            ITypeMapper<Source, Destination> mapper = new ValueMapper();
            var source = new Source { Id = 17, Updated = 41, Reuse = true };
            Destination.Constructions = 0;
            var created = mapper.Create(source);
            if (created.Id != 17 || created.Updated != 41 || Destination.Constructions != 1 ||
                source.Observations != 0)
                throw new InvalidOperationException("Create did not construct and initialize the value.");

            Destination.Constructions = 0;
            var original = default(Destination);
            var reused = mapper.Update(source, original);
            if (reused.Id != 0 || reused.Updated != 41 || original.Updated != 0 || Destination.Constructions != 0 ||
                source.Observations != 1 || source.LastReadId != 0 || source.LastReadText != "0")
                throw new InvalidOperationException("Default is an available previous value; member updates must reach the returned copy.");

            source.Reuse = false;
            var replaced = mapper.Update(source, original);
            if (replaced.Id != 17 || replaced.Updated != 41 || Destination.Constructions != 1)
                throw new InvalidOperationException("Replacement did not preserve the constructor and member rules.");
        }

        public static void VerifyNullableValues()
        {
            ITypeMapper<Source, Destination?> mapper = new NullableMapper();
            var source = new Source { Id = 19, Updated = 43, Reuse = true };
            Destination.Constructions = 0;
            var created = mapper.Create(source);
            var fromNull = mapper.Update(source, null);
            if (created?.Id != 19 || fromNull?.Updated != 43 || Destination.Constructions != 2)
                throw new InvalidOperationException("Nullable absence did not use construction.");

            Destination.Constructions = 0;
            var reused = mapper.Update(source, default(Destination));
            if (reused?.Id != 0 || reused?.Updated != 43 || Destination.Constructions != 0)
                throw new InvalidOperationException("Nullable default value was confused with absence.");
        }

        public static void VerifySuppressedInvalidResults()
        {
            ITypeMapper<Source, ReferenceDestination> resolve = new InvalidResolveMapper();
            ITypeMapper<Source, ReferenceDestination> construct = new InvalidConstructMapper();
            var source = new Source { Id = 23, Updated = 47 };
            var previous = new ReferenceDestination(5);
            ReferenceDestination.Constructions = 0;
            if (!ReferenceEquals(previous, resolve.Update(source, previous)) ||
                !ReferenceEquals(previous, construct.Update(source, previous)))
                throw new InvalidOperationException("A valid reuse path was lost after diagnostic suppression.");
            ExpectConfiguration(() => resolve.Create(source));
            ExpectConfiguration(() => resolve.Update(source, null));
            ExpectConfiguration(() => construct.Create(source));
            ExpectConfiguration(() => construct.Update(source, null));
            if (ReferenceDestination.Constructions != 0)
                throw new InvalidOperationException("An invalid structured result was evaluated.");
        }

        public static void VerifySuppressedUnavailableReads()
        {
            ITypeMapper<Source, ReferenceDestination> mapper = new InvalidReadMapper();
            var source = new Source { Id = 23 };
            var previous = new ReferenceDestination(5);
            ReferenceDestination.Constructions = 0;
            ExpectConfiguration(() => mapper.Create(source));
            ExpectConfiguration(() => mapper.Update(source, null));
            if (ReferenceDestination.Constructions != 0)
                throw new InvalidOperationException("Unavailable reads reached construction.");
            var replacement = mapper.Update(source, previous);
            if (ReferenceEquals(previous, replacement) || replacement.Id != 5 ||
                ReferenceDestination.Constructions != 1)
                throw new InvalidOperationException("A valid constructor read was lost after diagnostic suppression.");
        }

        private static void ExpectConfiguration(Action action)
        {
            try { action(); }
            catch (MappingConfigurationException) { return; }
            throw new InvalidOperationException("An invalid result did not use typed recovery.");
        }
    }
}
