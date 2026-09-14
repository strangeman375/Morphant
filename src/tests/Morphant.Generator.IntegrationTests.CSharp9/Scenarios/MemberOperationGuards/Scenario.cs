#nullable enable
#pragma warning disable CS1591
using System;
using System.Collections.Generic;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MemberOperationGuards
{
    public enum GuardKind { UpdateOnly, CreateOnly, OperationFirstOr, CallFirstOr, OperationFirstAnd, CallFirstAnd, UserDefinedAnd }
    public sealed class UpdateOnly { }
    public sealed class CreateOnly { }
    public sealed class OperationFirstOr { }
    public sealed class CallFirstOr { }
    public sealed class OperationFirstAnd { }
    public sealed class CallFirstAnd { }
    public sealed class UserDefinedAnd { }

    public readonly struct Truth
    {
        public static implicit operator Truth(bool value) => default;
        public static Truth operator &(Truth left, Truth right) => default;
        public static bool operator true(Truth value) => true;
        public static bool operator false(Truth value) => false;
    }

    public sealed class Source
    {
        public readonly List<string> Events = new List<string>();
        public bool Allowed { get; set; }
        public string FromConstruct() { Events.Add("construct"); return "constructor"; }
        public string FromMembers() { Events.Add("member"); return "member"; }
        public bool ShouldMap() { Events.Add("condition"); return Allowed; }
        public Truth CustomCondition() { Events.Add("condition"); return default; }
    }

    public sealed class Destination<T>
    {
        public Destination(string name) => Name = name;
        public string Name { get; set; }
    }

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination<UpdateOnly>>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.FromConstruct()))
                .Members((source, previous, result, context) =>
                {
                    if (context.Operation == MappingOperation.Update)
                        return new() { Name = source.FromMembers() };
                    return new() { Name = Ignore() };
                });
            builder.Map<Source, Destination<CreateOnly>>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.FromConstruct()))
                .Members((source, previous, result, context) =>
                {
                    if (context.Operation != MappingOperation.Update)
                        return new() { Name = source.FromMembers() };
                    return new() { Name = Ignore() };
                });
            builder.Map<Source, Destination<OperationFirstOr>>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.FromConstruct()))
                .Members((source, previous, result, context) =>
                {
                    if (context.Operation == MappingOperation.Update || source.ShouldMap())
                        return new() { Name = source.FromMembers() };
                    return new() { Name = Ignore() };
                });
            builder.Map<Source, Destination<CallFirstOr>>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.FromConstruct()))
                .Members((source, previous, result, context) =>
                {
                    if (source.ShouldMap() || context.Operation == MappingOperation.Update)
                        return new() { Name = source.FromMembers() };
                    return new() { Name = Ignore() };
                });
            builder.Map<Source, Destination<OperationFirstAnd>>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.FromConstruct()))
                .Members((source, previous, result, context) =>
                {
                    if (context.Operation != MappingOperation.Update && source.ShouldMap())
                        return new() { Name = source.FromMembers() };
                    return new() { Name = Ignore() };
                });
            builder.Map<Source, Destination<CallFirstAnd>>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.FromConstruct()))
                .Members((source, previous, result, context) =>
                {
                    if (source.ShouldMap() && context.Operation != MappingOperation.Update)
                        return new() { Name = source.FromMembers() };
                    return new() { Name = Ignore() };
                });
            builder.Map<Source, Destination<UserDefinedAnd>>()
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.FromConstruct()))
                .Members((source, previous, result, context) =>
                {
                    if (context.Operation != MappingOperation.Update && source.CustomCondition())
                        return new() { Name = source.FromMembers() };
                    return new() { Name = Ignore() };
                });
        }
    }

    public static class Scenario
    {
        public static void Verify(GuardKind kind, string operation, bool allowed)
        {
            var mapper = new Mapper();
            switch (kind)
            {
                case GuardKind.UpdateOnly:
                    Verify((ITypeMapper<Source, Destination<UpdateOnly>>)mapper, kind, operation, allowed);
                    break;
                case GuardKind.CreateOnly:
                    Verify((ITypeMapper<Source, Destination<CreateOnly>>)mapper, kind, operation, allowed);
                    break;
                case GuardKind.OperationFirstOr:
                    Verify((ITypeMapper<Source, Destination<OperationFirstOr>>)mapper, kind, operation, allowed);
                    break;
                case GuardKind.CallFirstOr:
                    Verify((ITypeMapper<Source, Destination<CallFirstOr>>)mapper, kind, operation, allowed);
                    break;
                case GuardKind.OperationFirstAnd:
                    Verify((ITypeMapper<Source, Destination<OperationFirstAnd>>)mapper, kind, operation, allowed);
                    break;
                case GuardKind.CallFirstAnd:
                    Verify((ITypeMapper<Source, Destination<CallFirstAnd>>)mapper, kind, operation, allowed);
                    break;
                case GuardKind.UserDefinedAnd:
                    Verify((ITypeMapper<Source, Destination<UserDefinedAnd>>)mapper, kind, operation, allowed);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static void Verify<T>(ITypeMapper<Source, Destination<T>> mapper, GuardKind kind, string operation, bool allowed)
        {
            var source = new Source { Allowed = allowed };
            var existing = new Destination<T>("existing");
            var result = operation switch
            {
                "Create" => mapper.Create(source),
                "UpdateNull" => mapper.Update(source, null),
                "UpdateExisting" => mapper.Update(source, existing),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            var update = operation != "Create";
            var selected = kind switch
            {
                GuardKind.UpdateOnly => update,
                GuardKind.CreateOnly => !update,
                GuardKind.UserDefinedAnd => true,
                GuardKind.OperationFirstOr or GuardKind.CallFirstOr => update || allowed,
                _ => !update && allowed
            };
            var callsCondition = kind switch
            {
                GuardKind.UpdateOnly or GuardKind.CreateOnly => false,
                GuardKind.OperationFirstOr or GuardKind.OperationFirstAnd => !update,
                _ => true
            };
            var expectedEvents = new List<string>();
            if (operation != "UpdateExisting") expectedEvents.Add("construct");
            if (callsCondition) expectedEvents.Add("condition");
            if (selected) expectedEvents.Add("member");
            var expectedName = selected ? "member" : operation == "UpdateExisting" ? "existing" : "constructor";

            if (result.Name != expectedName ||
                ReferenceEquals(result, existing) != (operation == "UpdateExisting") ||
                string.Join(",", source.Events) != string.Join(",", expectedEvents))
                throw new InvalidOperationException("Operation specialization changed member selection, reuse, or condition effects.");
        }
    }
}
