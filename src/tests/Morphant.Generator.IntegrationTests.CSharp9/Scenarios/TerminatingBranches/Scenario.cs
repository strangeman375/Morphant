#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.TerminatingBranches
{
    public enum Route { Construct, Resolve, Members }

    public sealed class Source
    {
        public int Mode { get; set; }
        public List<string> Events { get; } = new List<string>();
        public int Read(string step) { Events.Add(step); return 7; }
        public Exception Fail(string branch)
        {
            Events.Add("throw:" + branch);
            return new InvalidOperationException(branch);
        }
    }

    public sealed class Destination
    {
        private readonly Source owner;
        private int value;
        public Destination(Source owner, int value)
        {
            this.owner = owner;
            this.value = value;
            owner.Events.Add("construct");
        }
        public int Value
        {
            get => value;
            set { owner.Events.Add("set"); this.value = value; }
        }
    }

    [MorphantMapper]
    public partial class ConstructMapper : TypeMapper<ConstructMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source =>
                {
                    if (source.Mode >= 0)
                        return new(source, source.Read("constructor"));
                    throw source.Fail("fallback");
                })
                .Members(source => new() { Value = source.Read("member") });
    }

    [MorphantMapper]
    public partial class ResolveMapper : TypeMapper<ResolveMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Resolve((source, previous) =>
                {
                    if (source.Mode >= 0)
                    {
                        if (source.Mode == 0 && previous.HasValue)
                            return previous.Value;
                        return new(source, source.Read("constructor"));
                    }
                    throw source.Fail("fallback");
                })
                .Members(source => new() { Value = source.Read("member") });
    }

    [MorphantMapper]
    public partial class MembersMapper : TypeMapper<MembersMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Construct(source => new(source, source.Read("constructor")))
                .Members(source =>
                {
                    if (source.Mode == 0)
                        return new() { Value = source.Read("member") };
                    if (source.Mode > 0)
                    {
                        if (source.Mode == 1)
                        {
                            var value = source.Read("nested");
                            return new() { Value = value };
                        }
                        throw source.Fail("inner");
                    }
                    throw source.Fail("fallback");
                });
    }

    public static class Scenario
    {
        public static void Verify(Route route, int mode, string createEvents, string updateEvents)
        {
            ITypeMapper<Source, Destination> mapper = route switch
            {
                Route.Construct => new ConstructMapper(),
                Route.Resolve => new ResolveMapper(),
                Route.Members => new MembersMapper(),
                _ => throw new ArgumentOutOfRangeException(nameof(route))
            };

            foreach (var update in new[] { false, true })
            {
                var source = new Source { Mode = mode };
                var previous = new Destination(source, -1);
                source.Events.Clear();
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

                var expected = update ? updateEvents : createEvents;
                var actual = string.Join(",", source.Events);
                if (actual != expected)
                    throw new InvalidOperationException($"Unexpected branch evaluations: {actual}; expected {expected}.");

                var throwIndex = expected.IndexOf("throw:", StringComparison.Ordinal);
                var expectedFailure = throwIndex < 0 ? null : expected.Substring(throwIndex + 6);
                if (failure != expectedFailure)
                    throw new InvalidOperationException($"Unexpected branch exception: {failure}; expected {expectedFailure}.");

                if (expectedFailure is null)
                {
                    var reuse = update && (route != Route.Resolve || mode == 0);
                    if (result?.Value != 7 || ReferenceEquals(result, previous) != reuse)
                        throw new InvalidOperationException("The selected branch returned an incorrect destination.");
                }
                else if (previous.Value != -1)
                {
                    throw new InvalidOperationException("A throwing branch mutated the previous destination.");
                }
            }
        }
    }
}
