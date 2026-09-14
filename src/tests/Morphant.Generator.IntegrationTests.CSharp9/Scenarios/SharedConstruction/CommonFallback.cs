#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SharedConstruction
{
    public sealed class GuardSource
    {
        public List<string> Events { get; } = new List<string>();
        public bool BeforeResult { get; init; }
        public bool AfterResult { get; init; }
        public bool LastResult { get; init; }
        public int Id { get; init; }
        public bool Before(out int id) { Events.Add("before"); id = Id; return BeforeResult; }
        public bool Before() { Events.Add("before"); return BeforeResult; }
        public bool After(int id) { Events.Add("after:" + id); return AfterResult; }
        public bool After() { Events.Add("after"); return AfterResult; }
        public bool Last() { Events.Add("last"); return LastResult; }
        public int Read() { Events.Add("read"); return Id; }
    }

    public sealed class GuardDestination
    {
        public GuardDestination(int id) => Id = id;
        public int Id { get; }
    }

    [MorphantMapper]
    public partial class OutGuardMapper : TypeMapper<OutGuardMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<GuardSource, GuardDestination>()
                .Resolve((source, previous) =>
                {
                    if (source.Before(out var id) && previous.HasValue && source.After(id) && source.Last())
                        return previous.Value;
                    return new(id);
                });
    }

    [MorphantMapper]
    public partial class ScopedGuardMapper : TypeMapper<ScopedGuardMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<GuardSource, GuardDestination>()
                .Resolve((source, previous) =>
                {
                    if (source.Before() && previous.HasValue && source.After())
                    {
                        var id = source.Read();
                        if (id < 0)
                            throw new InvalidOperationException("blocked reuse");
                        return previous.Value;
                    }
                    else
                    {
                        var id = source.Read();
                        return new(id);
                    }
                });
    }

    public static partial class Scenario
    {
        public static void VerifyOutGuard(int operation, bool before, bool after, bool last)
        {
            var source = new GuardSource { Id = 42, BeforeResult = before, AfterResult = after, LastResult = last };
            var previous = new GuardDestination(-1);
            var mapper = (ITypeMapper<GuardSource, GuardDestination>)new OutGuardMapper();
            var result = operation == 0 ? mapper.Create(source, default) :
                mapper.Update(source, operation == 1 ? null : previous, default);
            var expected = new List<string> { "before" };
            if (operation == 2 && before)
            {
                expected.Add("after:42");
                if (after) expected.Add("last");
            }
            var reuse = operation == 2 && before && after && last;
            if (string.Join(",", source.Events) != string.Join(",", expected) ||
                ReferenceEquals(result, previous) != reuse || result.Id != (reuse ? -1 : 42))
                throw new InvalidOperationException("Shared fallback lost the out value or changed guard execution.");
        }

        public static void VerifyScopedGuard(int operation, bool before, bool after, int id)
        {
            var source = new GuardSource { Id = id, BeforeResult = before, AfterResult = after };
            var previous = new GuardDestination(99);
            var mapper = (ITypeMapper<GuardSource, GuardDestination>)new ScopedGuardMapper();
            GuardDestination? result = null;
            string? failure = null;
            try
            {
                result = operation == 0 ? mapper.Create(source, default) :
                    mapper.Update(source, operation == 1 ? null : previous, default);
            }
            catch (InvalidOperationException exception) { failure = exception.Message; }

            var expected = new List<string> { "before" };
            if (operation == 2 && before) expected.Add("after");
            expected.Add("read");
            var reuse = operation == 2 && before && after;
            if (string.Join(",", source.Events) != string.Join(",", expected))
                throw new InvalidOperationException("Shared fallback changed the selected local evaluation.");
            if (reuse && id < 0)
            {
                if (failure != "blocked reuse" || result is not null)
                    throw new InvalidOperationException("The reuse failure did not terminate the mapping.");
            }
            else if (failure is not null || result is null ||
                     ReferenceEquals(result, previous) != reuse || result.Id != (reuse ? 99 : id))
                throw new InvalidOperationException("Shared fallback returned the wrong local value.");
        }
    }
}
