#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.OperationDispatch
{
    public sealed class Source
    {
        public readonly List<string> Events = new List<string>();
        public bool Reuse { get; set; }
        public bool ConditionResult { get; set; }
        public bool ThrowCondition { get; set; }

        public string ReadLabel(string operation)
        {
            Events.Add(operation);
            return operation + ":source";
        }

        public bool Probe()
        {
            Events.Add("condition");
            if (ThrowCondition)
                throw new ProbeException();
            return ConditionResult;
        }
    }

    public sealed class ProbeException : Exception { }

    public sealed class Destination
    {
        public Destination(string label) => Label = label;
        public string Label { get; }
        public string Trace { get; set; } = "initial";
    }

    [MorphantMapper]
    public partial class ConstructMapper : TypeMapper<ConstructMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct((source, context) => new(
                    context.Operation == MappingOperation.Create
                        ? source.ReadLabel("create")
                        : source.ReadLabel("update")))
                .Members((source, previous, result, context) =>
                {
                    if (source.Probe())
                        return new()
                        {
                            Trace = (previous.HasValue ? previous.Value.Label : "none")
                                + "|" + result.Label
                                + "|" + context.Operation
                        };
                    return new()
                    {
                        Trace = (previous.HasValue ? previous.Value.Label : "none")
                            + "|" + result.Label
                            + "|" + context.Operation
                    };
                });
    }

    [MorphantMapper]
    public partial class ResolveMapper : TypeMapper<ResolveMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .Resolve((source, previous, context) =>
                {
                    if (previous.TryGetValue(out var current) && source.Reuse)
                        return current;
                    return new(
                        context.Operation == MappingOperation.Create
                            ? source.ReadLabel("create")
                            : source.ReadLabel("update"));
                })
                .Members((source, previous, result, context) =>
                {
                    if (source.Probe())
                        return new()
                        {
                            Trace = (previous.HasValue ? previous.Value.Label : "none")
                                + "|" + result.Label
                                + "|" + context.Operation
                        };
                    return new()
                    {
                        Trace = (previous.HasValue ? previous.Value.Label : "none")
                            + "|" + result.Label
                            + "|" + context.Operation
                    };
                });
    }

    public static class Scenario
    {
        public static void Verify(
            bool resolve, string operation, bool conditionResult, bool throwCondition)
        {
            ITypeMapper<Source, Destination> mapper = resolve
                ? new ResolveMapper()
                : new ConstructMapper();
            var source = new Source
            {
                Reuse = operation == "Reuse",
                ConditionResult = conditionResult,
                ThrowCondition = throwCondition
            };
            var existing = operation == "Reuse" || operation == "Replace"
                ? new Destination("existing")
                : null;
            bool create = operation == "Create";
            bool reuse = existing is not null && (!resolve || source.Reuse);
            string expectedLabel = reuse ? "existing" : create ? "create:source" : "update:source";
            string expectedTrace = (existing is null ? "none" : "existing")
                + "|" + expectedLabel + "|" + (create ? "Create" : "Update");
            Destination? result = null;
            bool threw = false;
            try
            {
                result = create ? mapper.Create(source) : mapper.Update(source, existing);
            }
            catch (ProbeException)
            {
                threw = true;
            }

            string expectedEvents = reuse ? "condition" : create ? "create,condition" : "update,condition";
            if (string.Join(",", source.Events) != expectedEvents || threw != throwCondition)
                throw new InvalidOperationException("Condition evaluation order, count or exception changed.");
            if (!threw && (result is null || result.Label != expectedLabel || result.Trace != expectedTrace
                || ReferenceEquals(result, existing) != reuse))
                throw new InvalidOperationException("Operation, previous destination or selected result changed.");
            if (existing is not null && existing.Trace != (!threw && reuse ? expectedTrace : "initial"))
                throw new InvalidOperationException("The previous destination was mutated unexpectedly.");
        }
    }
}
