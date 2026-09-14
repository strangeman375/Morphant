#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TupleLiteralComposition
{
    public sealed class Source
    {
        public readonly List<string> Events = new List<string>();
        public int State = 3;
        public bool Branch;
        public int ReadId() { Events.Add("id"); return State; }
        public Converted ReadConversion() { Events.Add("operand"); return new Converted(this); }
        public string InitialName() { Events.Add("constructor"); State = 5; return "initial"; }
        public bool Choose() { Events.Add("condition"); return Branch; }
        public string FinalName() { Events.Add("member"); State = 99; return "final"; }
    }

    public readonly struct Converted
    {
        private readonly Source source;
        public Converted(Source source) => this.source = source;
        public static implicit operator int(Converted value)
        {
            value.source.Events.Add("conversion");
            return value.source.State * 10;
        }
    }

    [MorphantMapper]
    public partial class ValueMapper : TypeMapper<ValueMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, (int Fixed, int Id, int Converted, string Name)>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(7, source.ReadId(), Value<int>(source.ReadConversion()), source.InitialName()))
                .Members(source =>
                {
                    if (source.Choose())
                        return new() { Name = source.FinalName() };
                    return new() { Name = Ignore() };
                });
    }

    [MorphantMapper]
    public partial class ReferenceMapper : TypeMapper<ReferenceMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Tuple<int, int, int, string>>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(7, source.ReadId(), Value<int>(source.ReadConversion()), source.InitialName()))
                .Members(source =>
                {
                    if (source.Choose())
                        return new() { Item4 = source.FinalName() };
                    return new() { Item4 = Ignore() };
                });
    }

    public static class Scenario
    {
        public static void Verify(bool referenceTuple, bool update, bool branch, bool nullDestination = false)
        {
            var source = new Source { Branch = branch };
            var creates = !update || nullDestination;
            (int Fixed, int Id, int Converted, string Name) result;
            if (referenceTuple)
            {
                ITypeMapper<Source, Tuple<int, int, int, string>> mapper = new ReferenceMapper();
                var previous = nullDestination ? null : new Tuple<int, int, int, string>(101, 102, 103, "old");
                var mapped = update ? mapper.Update(source, previous) : mapper.Create(source);
                if (!creates && !ReferenceEquals(mapped, previous))
                    throw new InvalidOperationException("Updating a reference tuple must keep the supplied instance.");
                result = (mapped.Item1, mapped.Item2, mapped.Item3, mapped.Item4);
            }
            else
            {
                ITypeMapper<Source, (int Fixed, int Id, int Converted, string Name)> mapper = new ValueMapper();
                result = update ? mapper.Update(source, (101, 102, 103, "old")) : mapper.Create(source);
            }

            var updatesName = branch && (creates || !referenceTuple);
            var expectedName = updatesName ? "final" : creates ? "initial" : "old";
            var expected = creates ? (7, 3, 30, expectedName) : (101, 102, 103, expectedName);
            var events = (creates ? "id,operand,conversion,constructor," : "") +
                "condition" + (updatesName ? ",member" : "");
            if (result != expected || string.Join(",", source.Events) != events)
                throw new InvalidOperationException("Tuple composition changed element values or evaluation order.");
        }
    }
}
