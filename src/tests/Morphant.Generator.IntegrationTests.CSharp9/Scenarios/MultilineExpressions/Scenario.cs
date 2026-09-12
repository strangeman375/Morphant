#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MultilineExpressions
{
    public sealed class Source
    {
        public string Name { get; set; } = "allowed";
        public List<string> Events { get; } = new List<string>();
        public string Read(string step) { Events.Add(step); return step; }
    }

    public sealed class Destination
    {
        public Destination(string name) => Name = name;
        public string Name { get; set; }
    }

    [MorphantMapper]
    public partial class StructuredMapper : TypeMapper<StructuredMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(source.Read("constructor")))
                .Members(source =>
                {
                    var prefix = string.Concat(
                        @"first
  second",

                        // The spaces and quotes are part of the value.
                        @"  ""quoted""  ");
                    if (source.Name == "allowed")
                        return new()
                        {
                            Name = string.Concat(
                                prefix,
                                $"{{{source.Read("member")}}}\n\t")
                        };
                    throw new InvalidOperationException(
                        int.TryParse(source.Name, out int parsed)
                            ? parsed.ToString()
                            : source.Read("fallback"));
                });
    }

    [MorphantMapper]
    public partial class FactoryMapper : TypeMapper<FactoryMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructUsing(source => new Destination(
                    string.Concat(
                        @"first
  second",
                        @"  ""quoted""  ",
                        $"{{{source.Read("factory")}}}\n\t")))
                .Members(_ => new() { Name = Ignore() });
    }

    public static class Scenario
    {
        public static void Verify(bool factory, bool update, string name)
        {
            ITypeMapper<Source, Destination> mapper = factory
                ? new FactoryMapper()
                : new StructuredMapper();
            var source = new Source { Name = name };
            var previous = new Destination("previous");
            Destination? result = null;
            string? failure = null;
            try
            {
                result = update
                    ? mapper.Update(source, previous, default)
                    : mapper.Create(source, default);
            }
            catch (InvalidOperationException exception)
            {
                failure = exception.Message;
            }

            var expectedFailure = factory || name == "allowed"
                ? null : name == "42" ? "42" : "fallback";
            var expectedEvents = factory
                ? update ? "" : "factory"
                : (update ? "" : "constructor,") +
                  (name == "allowed" ? "member" : name == "42" ? "" : "fallback");
            expectedEvents = expectedEvents.TrimEnd(',');
            if (failure != expectedFailure || string.Join(",", source.Events) != expectedEvents)
                throw new InvalidOperationException("Expression evaluation order or selected throw branch changed.");

            // This independently compiled literal follows the consumer file's
            // line endings, including on platforms that check out CRLF files.
            var prefix = @"first
  second" + @"  ""quoted""  ";
            if (expectedFailure is null)
            {
                var expectedName = factory && update
                    ? "previous" : prefix + (factory ? "{factory}\n\t" : "{member}\n\t");
                if (result?.Name != expectedName || ReferenceEquals(result, previous) != update)
                    throw new InvalidOperationException("Literal values or destination reuse changed.");
            }
            else if (previous.Name != "previous")
            {
                throw new InvalidOperationException("A throwing expression mutated the destination.");
            }
        }
    }
}
