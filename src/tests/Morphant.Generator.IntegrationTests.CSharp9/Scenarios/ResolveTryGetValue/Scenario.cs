#nullable enable
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ResolveTryGetValue
{
    public sealed class Source
    {
        public int Id { get; set; }
        public int Comparisons { get; set; }
        public bool Matches(Destination value)
        {
            Comparisons++;
            return value.Id == Id;
        }
    }

    public sealed class Destination
    {
        public static int Constructions { get; set; }
        public Destination(int id) { Id = id; Constructions++; }
        public int Id { get; }
    }

    [MorphantMapper]
    public sealed partial class AndMapper : TypeMapper<AndMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Resolve((source, previous) =>
            {
                if (previous.TryGetValue(out var value) && source.Matches(value))
                    return value;
                return new(source.Id);
            });
    }

    [MorphantMapper]
    public sealed partial class NestedMapper : TypeMapper<NestedMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Resolve((source, previous) =>
            {
                if (previous.TryGetValue(out var value))
                {
                    if (source.Matches(value)) return value;
                }
                return new(source.Id);
            });
    }

    [MorphantMapper]
    public sealed partial class ConditionalMapper : TypeMapper<ConditionalMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Resolve((source, previous) =>
                previous.TryGetValue(out var value) && source.Matches(value)
                    ? value
                    : new global::Morphant.Generated.N_67f09af83af980a4facb4ee28cd92d98.DestinationConstruction(source.Id));
    }

    [MorphantMapper]
    public sealed partial class NegatedMapper : TypeMapper<NegatedMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Resolve((source, previous) =>
            {
                if (!previous.TryGetValue(out var value)) return new(source.Id);
                if (source.Matches(value)) return value;
                return new(source.Id);
            });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            VerifyMapper(new AndMapper());
            VerifyMapper(new NestedMapper());
            VerifyMapper(new ConditionalMapper());
            VerifyMapper(new NegatedMapper());
        }

        private static void VerifyMapper(ITypeMapper<Source, Destination> mapper)
        {
            var source = new Source { Id = 11 };
            Destination.Constructions = 0;
            if (mapper.Create(source).Id != 11 || Destination.Constructions != 1 || source.Comparisons != 0)
                throw new InvalidOperationException("Create evaluated an unavailable destination.");
            Destination.Constructions = 0;
            if (mapper.Update(source, null).Id != 11 || Destination.Constructions != 1 || source.Comparisons != 0)
                throw new InvalidOperationException("Update(null) evaluated an unavailable destination.");
            var previous = new Destination(11);
            Destination.Constructions = 0;
            if (!ReferenceEquals(previous, mapper.Update(source, previous)) ||
                Destination.Constructions != 0 || source.Comparisons != 1)
                throw new InvalidOperationException("Reuse changed identity or evaluation count.");
            previous = new Destination(17);
            Destination.Constructions = 0;
            source.Comparisons = 0;
            var replacement = mapper.Update(source, previous);
            if (ReferenceEquals(previous, replacement) || replacement.Id != 11 ||
                Destination.Constructions != 1 || source.Comparisons != 1)
                throw new InvalidOperationException("Replacement changed evaluation count.");
        }
    }
}
