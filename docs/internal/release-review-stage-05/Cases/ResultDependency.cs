// Verify MORPH0042 by default; SUPPRESS_RESULT_DIAGNOSTIC exercises recovery.
#if SUPPRESS_RESULT_DIAGNOSTIC
#pragma warning disable MORPH0042
#endif
using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Stage05Audit.Cases
{
    public sealed class Source { public int Value { get; set; } = 7; }
    public sealed class Destination
    {
        private int value;
        public Destination(int value) { ConstructorValue = value; Value = value; }
        public int ConstructorValue { get; }
        public int Writes { get; private set; }
        public int Value { get => value; set { this.value = value; Writes++; } }
    }

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members((_, _, result) => new() { Value = result.Value + 10 });
    }

    public static class Scenario
    {
        public static void Run()
        {
            ITypeMapper<Source, Destination> mapper = new Mapper();
            ExpectFailure(() => mapper.Create(new Source()), MappingOperation.Create);
            ExpectFailure(() => mapper.Update(new Source(), null), MappingOperation.Update);
            var previous = new Destination(7);
            var result = mapper.Update(new Source(), previous);
            Check.Equal("reuses the existing destination", true, ReferenceEquals(previous, result));
            Check.Equal("existing destination constructor argument", 7, result.ConstructorValue);
            Check.Equal("existing destination member value", 17, result.Value);
            Check.Equal("existing destination assignments", 2, result.Writes);
        }

        private static void ExpectFailure(Func<Destination> action, MappingOperation operation)
        {
            try { action(); }
            catch (MappingConfigurationException exception)
            {
                Check.Equal("typed recovery operation", operation, exception.Operation);
                Check.Equal("typed recovery source", true, exception.SourceType == typeof(Source));
                Check.Equal("typed recovery destination", true, exception.DestinationType == typeof(Destination));
                return;
            }
            throw new InvalidOperationException("Invalid constructor dependency did not throw.");
        }
    }
}
