#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;
namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SwitchExhaustiveness
{
    public enum Choice { First, Second }
    public sealed class Source
    {
        public readonly List<string> Events = new List<string>();
        public int ChoiceValue { get; set; }
        public bool Guard { get; set; }
        public bool Reuse { get; set; }
        public bool Boolean { get { Events.Add("choice"); return ChoiceValue == 1; } }
        public bool? Nullable { get { Events.Add("choice"); return ChoiceValue == 2 ? (bool?)null : ChoiceValue == 1; } }
        public Choice Enum { get { Events.Add("choice"); return (Choice)ChoiceValue; } }
        public bool Probe() { Events.Add("guard"); return Guard; }
        public string First() { Events.Add("first"); return "first"; }
        public string Second() { Events.Add("second"); return "second"; }
        public string Name { get { Events.Add("member"); return "member"; } }
    }
    public sealed class Destination
    {
        public Destination(string name) => Name = name;
        public string Name { get; set; }
    }

    [MorphantMapper]
    public partial class ConstructCompleteMapper : TypeMapper<ConstructCompleteMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source =>
                {
                    return source.Boolean switch
                    {
                        true => new(source.First()),
                        false => new(source.Second())
                    };
                })
                .Members(source => new() { Name = source.Name });
    }

    #pragma warning disable CS8846 // Exercise the generated unmatched-value exception.

    [MorphantMapper]
    public partial class ConstructGuardedMapper : TypeMapper<ConstructGuardedMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source =>
                {
                    return source.Boolean switch
                    {
                        true when source.Probe() => new(source.First()),
                        false => new(source.Second())
                    };
                })
                .Members(source => new() { Name = source.Name });
    }
    #pragma warning restore CS8846

    #pragma warning disable CS8655 // Exercise the generated unmatched-value exception.

    [MorphantMapper]
    public partial class ConstructNullableMapper : TypeMapper<ConstructNullableMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source =>
                {
                    return source.Nullable switch
                    {
                        true => new(source.First()),
                        false => new(source.Second())
                    };
                })
                .Members(source => new() { Name = source.Name });
    }
    #pragma warning restore CS8655

    #pragma warning disable CS8524 // Exercise the generated unmatched-value exception.

    [MorphantMapper]
    public partial class ConstructEnumMapper : TypeMapper<ConstructEnumMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source =>
                {
                    return source.Enum switch
                    {
                        Choice.First => new(source.First()),
                        Choice.Second => new(source.Second())
                    };
                })
                .Members(source => new() { Name = source.Name });
    }
    #pragma warning restore CS8524

    [MorphantMapper]
    public partial class ResolveCompleteMapper : TypeMapper<ResolveCompleteMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Resolve((source, previous) =>
                {
                    if (previous.TryGetValue(out var existing) && source.Reuse) return existing;
                    return source.Boolean switch
                    {
                        true => new(source.First()),
                        false => new(source.Second())
                    };
                })
                .Members(source => new() { Name = source.Name });
    }

    #pragma warning disable CS8846 // Exercise the generated unmatched-value exception.

    [MorphantMapper]
    public partial class ResolveGuardedMapper : TypeMapper<ResolveGuardedMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Resolve((source, previous) =>
                {
                    if (previous.TryGetValue(out var existing) && source.Reuse) return existing;
                    return source.Boolean switch
                    {
                        true when source.Probe() => new(source.First()),
                        false => new(source.Second())
                    };
                })
                .Members(source => new() { Name = source.Name });
    }
    #pragma warning restore CS8846

    #pragma warning disable CS8655 // Exercise the generated unmatched-value exception.

    [MorphantMapper]
    public partial class ResolveNullableMapper : TypeMapper<ResolveNullableMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Resolve((source, previous) =>
                {
                    if (previous.TryGetValue(out var existing) && source.Reuse) return existing;
                    return source.Nullable switch
                    {
                        true => new(source.First()),
                        false => new(source.Second())
                    };
                })
                .Members(source => new() { Name = source.Name });
    }
    #pragma warning restore CS8655

    #pragma warning disable CS8524 // Exercise the generated unmatched-value exception.

    [MorphantMapper]
    public partial class ResolveEnumMapper : TypeMapper<ResolveEnumMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Resolve((source, previous) =>
                {
                    if (previous.TryGetValue(out var existing) && source.Reuse) return existing;
                    return source.Enum switch
                    {
                        Choice.First => new(source.First()),
                        Choice.Second => new(source.Second())
                    };
                })
                .Members(source => new() { Name = source.Name });
    }
    #pragma warning restore CS8524

    public static class Scenario
    {
        public static void Verify(bool resolve, string mode, string operation, int choice, bool guard)
        {
            ITypeMapper<Source, Destination> mapper = (resolve, mode) switch
            {
                (false, "Complete") => new ConstructCompleteMapper(),
                (false, "Guarded") => new ConstructGuardedMapper(),
                (false, "Nullable") => new ConstructNullableMapper(),
                (false, "Enum") => new ConstructEnumMapper(),
                (true, "Complete") => new ResolveCompleteMapper(),
                (true, "Guarded") => new ResolveGuardedMapper(),
                (true, "Nullable") => new ResolveNullableMapper(),
                (true, "Enum") => new ResolveEnumMapper(),
                _ => throw new ArgumentException(nameof(mode))
            };
            var source = new Source { ChoiceValue = choice, Guard = guard, Reuse = operation == "Reuse" };
            var previous = operation == "Reuse" || operation == "Replace" ? new Destination("previous") : null;
            bool reuse = previous is not null && (!resolve || source.Reuse);
            bool expectedThrow = !reuse && ((mode == "Guarded" && choice == 1 && !guard)
                || ((mode == "Nullable" || mode == "Enum") && choice == 2));
            string expected = reuse ? "member" : "choice"
                + (mode == "Guarded" && choice == 1 ? ",guard" : "")
                + (expectedThrow ? "" : (mode == "Enum" ? choice == 0 : choice == 1) ? ",first,member" : ",second,member");
            Destination? result = null;
            bool threw = false;
            try { result = operation == "Create" ? mapper.Create(source) : mapper.Update(source, previous); }
            catch (UnmatchedMappingSwitchException error)
            {
                threw = true;
                if (error.Operation != (operation == "Create" ? MappingOperation.Create : MappingOperation.Update))
                    throw new InvalidOperationException("Unmatched switch lost its operation.");
            }
            if (threw != expectedThrow || string.Join(",", source.Events) != expected)
                throw new InvalidOperationException("Switch evaluation changed: " + string.Join(",", source.Events) + "; expected: " + expected);
            if (!threw && (result is null || result.Name != "member" || ReferenceEquals(previous, result) != reuse))
                throw new InvalidOperationException("Switch result or reuse changed.");
            if (previous is not null && !reuse && previous.Name != "previous")
                throw new InvalidOperationException("A switch replacement mutated the previous destination.");
        }
    }
}
