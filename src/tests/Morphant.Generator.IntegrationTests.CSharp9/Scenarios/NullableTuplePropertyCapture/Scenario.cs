#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.NullableTuplePropertyCapture
{
    public sealed class Source
    {
        public readonly List<string> Events = new List<string>();
        public int State = 7;
        public int Id { get { Events.Add("id"); return State; } }
        public string FromConstruct() { Events.Add("constructor"); State = 11; return "constructor"; }
        public string FromMembers() { Events.Add("member"); State = 23; return "member"; }
    }

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, (int Id, string Name)?>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.Id, source.FromConstruct()))
                .Members(source => new() { Name = source.FromMembers() });
    }

    public static class Scenario
    {
        public static void Verify(string operation)
        {
            var source = new Source();
            ITypeMapper<Source, (int Id, string Name)?> mapper = new Mapper();
            (int Id, string Name)? result;
            (int Id, string Name)? expected;
            string events;
            switch (operation)
            {
                case "Create":
                    result = mapper.Create(source);
                    expected = (7, "member");
                    events = "id,constructor,member";
                    break;
                case "UpdateNull":
                    result = mapper.Update(source, null);
                    expected = (7, "member");
                    events = "id,constructor,member";
                    break;
                case "UpdateExisting":
                    result = mapper.Update(source, (91, "old"));
                    expected = (91, "member");
                    events = "member";
                    break;
                case "CreateNullSource":
                    result = mapper.Create(null);
                    expected = null;
                    events = "";
                    break;
                case "UpdateNullSource":
                    result = mapper.Update(null, (91, "old"));
                    expected = null;
                    events = "";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }

            if (result != expected || string.Join(",", source.Events) != events ||
                source.State != (expected.HasValue ? 23 : 7))
                throw new InvalidOperationException("Nullable tuple mapping moved or repeated the constructor property read.");
        }
    }
}
