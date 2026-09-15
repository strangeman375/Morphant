#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;
using Morphant.RuntimeSupport;
using DestinationMembers = Morphant.Generated.N_7b90cf053b7a6790fbfbf441dba532b7.DestinationMembers;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.CheckedNestedUpdate
{
    public interface IChildDestination { }
    public sealed class ChildDestination : IChildDestination { public int Value { get; set; } }
    public sealed class OtherChild : IChildDestination { }
    public sealed class SourceReadException : Exception { }
    public sealed record ChildSource(List<string> Events);

    public sealed class Source
    {
        public List<string> Events { get; } = new();
        public IChildDestination? Target { get; set; }
        public bool ThrowSource { get; set; }
        public Destination CreateDestination() => new(Target, Events);
        public ChildSource ReadChild()
        {
            Events.Add("source");
            if (ThrowSource) throw new SourceReadException();
            return new(Events);
        }
        public ChildSource ReadChild(ref int reads) { reads++; return ReadChild(); }
    }

    public sealed class Destination
    {
        private readonly List<string> _events;
        internal readonly IChildDestination? OriginalChild;
        public Destination(IChildDestination? child, List<string> events)
        {
            OriginalChild = child;
            _events = events;
        }
        public IChildDestination? Child
        {
            get { _events.Add("destination"); return OriginalChild; }
        }
        public int Observed { get; set; }
    }

    [MorphantMapper]
    public partial class StaticMapper : TypeMapper<StaticMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>()
                .Convert((source, previous, context) => Scenario.UpdateChild(source, previous, context));
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .ConstructUsing(source => source.CreateDestination())
                .Members((source, _) =>
                {
                    var members = new DestinationMembers();
                    Update<ChildDestination>(source.ReadChild(), members.Child);
                    return members;
                });
        }
    }

    [MorphantMapper]
    public partial class CapturedMapper : TypeMapper<CapturedMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ChildSource, ChildDestination>()
                .Convert((source, previous, context) => Scenario.UpdateChild(source, previous, context));
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .ConstructUsing(source => source.CreateDestination())
                .Members((source, _) =>
                {
                    var reads = 0;
                    var members = new DestinationMembers();
                    Update<ChildDestination>(source.ReadChild(ref reads), members.Child);
                    return members with { Observed = reads };
                });
        }
    }

    public static class Scenario
    {
        public static ChildDestination UpdateChild(ChildSource? source, Option<ChildDestination> previous, MappingContext context)
        {
            if (source is null || !previous.HasValue || context.Operation != MappingOperation.Update)
                throw new InvalidOperationException("The child must receive Update with its existing destination.");
            source.Events.Add("update");
            previous.Value.Value = 42;
            return new ChildDestination { Value = -1 };
        }

        public static void Verify(bool capture, string operation, int destinationKind, bool throwSource)
        {
            var source = NewSource(destinationKind);
            source.ThrowSource = throwSource;
            var previous = operation == "UpdateExisting" ? source.CreateDestination() : null;
            ITypeMapper<Source, Destination> mapper = capture ? new CapturedMapper() : new StaticMapper();
            Destination? result = null;
            Exception? failure = null;
            try { result = operation == "Create" ? mapper.Create(source) : mapper.Update(source, previous); }
            catch (Exception exception) { failure = exception; }

            var expectedFailure = destinationKind == 0 ? null : throwSource
                ? typeof(SourceReadException) : destinationKind == 2 ? typeof(NestedDestinationTypeMismatchException) : null;
            if (failure?.GetType() != expectedFailure)
                throw new InvalidOperationException("The source failure or destination mismatch occurred in the wrong order.", failure);
            if (failure is NestedDestinationTypeMismatchException mismatch) VerifyMismatch(mismatch);
            var expectedEvents = destinationKind == 0 ? "destination" :
                expectedFailure is null ? "destination,source,update" : "destination,source";
            if (string.Join(",", source.Events) != expectedEvents)
                throw new InvalidOperationException("Wrong evaluation order: " + string.Join(",", source.Events));
            if (expectedFailure is null && (result is null ||
                previous is not null && !ReferenceEquals(result, previous) ||
                !ReferenceEquals(result.OriginalChild, source.Target) ||
                source.Target is ChildDestination child && child.Value != 42 ||
                result.Observed != (capture && destinationKind != 0 ? 1 : 0)))
                throw new InvalidOperationException("An instance was replaced or a selector mutation was lost.");
        }

        public static void VerifyContextAccess(bool capture, int destinationKind)
        {
            var source = NewSource(destinationKind);
            var reads = 0;
            Exception? failure = null;
            try
            {
                if (capture)
                    MappingHelpers.UpdateInPlace<ChildSource, ChildDestination, IChildDestination>(
                        source.Target, () => source.ReadChild(ref reads), default);
                else
                    MappingHelpers.UpdateInPlace<Source, ChildSource, ChildDestination, IChildDestination>(
                        source.Target, source, static s => s.ReadChild(), default);
            }
            catch (Exception exception) { failure = exception; }
            var expected = destinationKind == 0 ? null : destinationKind == 1
                ? typeof(InvalidMappingContextException) : typeof(NestedDestinationTypeMismatchException);
            if (failure?.GetType() != expected || string.Join(",", source.Events) != (destinationKind == 0 ? "" : "source") ||
                reads != (capture && destinationKind != 0 ? 1 : 0))
                throw new InvalidOperationException("Context access preceded source evaluation or destination validation.", failure);
        }

        private static Source NewSource(int destinationKind) => new()
        {
            Target = destinationKind == 0 ? null : destinationKind == 1 ? new ChildDestination() : new OtherChild()
        };

        private static void VerifyMismatch(NestedDestinationTypeMismatchException exception)
        {
            if (exception.Operation != MappingOperation.Update || exception.SourceType != typeof(ChildSource) ||
                exception.DestinationType != typeof(ChildDestination) || exception.ActualDestinationType != typeof(OtherChild))
                throw new InvalidOperationException("The destination mismatch lost its mapping pair or runtime type.");
        }
    }
}
