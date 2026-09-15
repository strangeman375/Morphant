#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CustomPreviousGuard
{
    public sealed class Source
    {
        public readonly List<string> Events = new List<string>();
        public Truth Flag { get { Events.Add("flag"); return new Truth(this, true); } }
    }
    public readonly struct Truth
    {
        private readonly Source? _owner;
        private readonly bool _value;
        public Truth(Source? owner, bool value) { _owner = owner; _value = value; }
        public static implicit operator Truth(bool value) => new Truth(null, value);
        public static bool operator true(Truth value) => value._value;
        public static bool operator false(Truth value) => false;
        public static Truth operator &(Truth left, Truth right)
        {
            var owner = left._owner ?? right._owner!;
            owner.Events.Add("and");
            return new Truth(owner, true);
        }
    }
    public sealed class Destination
    {
        public Destination(string value) { Value = value; }
        public string Value { get; set; }
    }
    [MorphantMapper]
    public partial class ResolveBeforeMapper : TypeMapper<ResolveBeforeMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) =>
            {
                if (previous.TryGetValue(out var _) && source.Flag) return new("selected");
                return new("fallback");
            });
    }
    [MorphantMapper]
    public partial class ResolveAfterMapper : TypeMapper<ResolveAfterMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) =>
            {
                if (source.Flag && previous.TryGetValue(out var _)) return new("selected");
                return new("fallback");
            });
    }
    [MorphantMapper]
    public partial class ConstructMembersBeforeMapper : TypeMapper<ConstructMembersBeforeMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Construct(source => new("seed"))
            .Members((source, previous) =>
            {
                if (previous.TryGetValue(out var _) && source.Flag) return new() { Value = "selected" };
                return new() { Value = "fallback" };
            });
    }
    [MorphantMapper]
    public partial class ConstructMembersAfterMapper : TypeMapper<ConstructMembersAfterMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Construct(source => new("seed"))
            .Members((source, previous) =>
            {
                if (source.Flag && previous.TryGetValue(out var _)) return new() { Value = "selected" };
                return new() { Value = "fallback" };
            });
    }
    [MorphantMapper]
    public partial class ResolveMembersBeforeMapper : TypeMapper<ResolveMembersBeforeMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) => new("seed"))
            .Members((source, previous) =>
            {
                if (previous.TryGetValue(out var _) && source.Flag) return new() { Value = "selected" };
                return new() { Value = "fallback" };
            });
    }
    [MorphantMapper]
    public partial class ResolveMembersAfterMapper : TypeMapper<ResolveMembersAfterMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>()
            .Resolve((source, previous) => new("seed"))
            .Members((source, previous) =>
            {
                if (source.Flag && previous.TryGetValue(out var _)) return new() { Value = "selected" };
                return new() { Value = "fallback" };
            });
    }
    public static class Scenario
    {
        public static void Verify(string policy, bool before, int operation)
        {
            var source = new Source();
            ITypeMapper<Source, Destination> mapper = (policy, before) switch
            {
                ("Resolve", true) => new ResolveBeforeMapper(), ("Resolve", false) => new ResolveAfterMapper(),
                ("ConstructMembers", true) => new ConstructMembersBeforeMapper(), ("ConstructMembers", false) => new ConstructMembersAfterMapper(),
                ("ResolveMembers", true) => new ResolveMembersBeforeMapper(), ("ResolveMembers", false) => new ResolveMembersAfterMapper(),
                _ => throw new ArgumentOutOfRangeException(nameof(policy))
            };
            var previous = new Destination("old");
            var result = operation == 0 ? mapper.Create(source) : mapper.Update(source, operation == 1 ? null : previous);
            if (result.Value != "selected" || string.Join(",", source.Events) != "flag,and" ||
                ReferenceEquals(result, previous) != (operation == 2 && policy == "ConstructMembers"))
                throw new InvalidOperationException("A custom logical operator was treated as Boolean short circuiting.");
        }
    }
}
