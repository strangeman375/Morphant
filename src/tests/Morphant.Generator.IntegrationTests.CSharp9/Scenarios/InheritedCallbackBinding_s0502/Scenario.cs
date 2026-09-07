#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InheritedCallbackBinding_s0502
{
    public enum CallbackKind { Construct, Resolve, Members, ConstructUsing, ResolveUsing, Convert }
    public sealed class Source { }
    public sealed class ConstructTag { }
    public sealed class ResolveTag { }
    public sealed class MembersTag { }
    public sealed class ConstructUsingTag { }
    public sealed class ResolveUsingTag { }
    public sealed class ConvertTag { }
    public sealed class QualifiedConstructTag { }
    public sealed class QualifiedResolveTag { }
    public sealed class QualifiedMembersTag { }
    public sealed class QualifiedConstructUsingTag { }
    public sealed class QualifiedResolveUsingTag { }
    public sealed class QualifiedConvertTag { }
    public sealed class ControlTag { }
    public sealed class ConvertGroupTag { }
    public sealed class FactoryGroupTag { }
    public sealed class ResolverGroupTag { }
    public sealed class DelegateTag { }
    public sealed class CovariantTag { }
    public sealed class ThisTag { }
    public sealed class InferredGenericTag { }
    public class Payload { }
    public sealed class DerivedPayload : Payload { }
    public sealed class Destination<TTag>
    {
        public Destination(string text) { Text = text; }
        public string Text { get; }
    }
    public sealed class MemberDestination<TTag> { public string Text { get; set; } = ""; }
    public sealed class DeferredDestination
    {
        public DeferredDestination(Func<string> read) { Read = read; }
        public Func<string> Read { get; }
    }

    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : BaseMapper<TMapper>
    {
        public int ReadCalls { get; private set; }
        public int DelegateReads { get; private set; }
        protected string Read() { ReadCalls++; return "base"; }
        protected string Field = "base-field";
        protected string Property => "base-property";
        public string PublicField = "base-public-field";
        public string PublicProperty => "base-public-property";
        protected static string ReadStatic() => "base-static";
        protected virtual string ReadVirtual() => "base-virtual";
        public virtual string HiddenVirtual() => "base-virtual-slot";
        protected string Overload(object value) => "base-object";
        protected string Generic<TValue>(TValue value) => typeof(TValue).Name;
        protected virtual Payload ReadPayload() => new();
        protected virtual Payload Payload => new();
        protected static string Describe(Payload value) => value is DerivedPayload
            ? "base-parameter" : "base-instance";
        protected static string Describe(DerivedPayload value) => "derived-parameter";
        protected static string DescribeMapper(BaseMapper<TMapper> mapper) => "base-type";
        protected static string DescribeMapper(TMapper mapper) => "self-type";
        protected string Identify<TValue>(TValue value) => typeof(TValue).Name;
        protected string Identify(Mapper value) => "nongeneric";
        protected Destination<ConvertGroupTag> ReadConverted(Source? source) => new(Read());
        protected Destination<FactoryGroupTag> ReadConstructed(Source source) => new(Read());
        protected Destination<ResolverGroupTag> ReadResolved(Source source, Option<Destination<ResolverGroupTag>> previous) => new(Read());
        protected Morphant.Delegates.Convert<Source?, Destination<DelegateTag>> Converter
        {
            get { DelegateReads++; return _ => new(Read()); }
        }

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, Destination<ConstructTag>>().Construct(_ => new(Read()));
            builder.Map<Source, Destination<ResolveTag>>().Resolve((_, previous) => new(Read()));
            builder.Map<Source, MemberDestination<MembersTag>>().Members(_ => new() { Text = Read() });
            builder.Map<Source, Destination<ConstructUsingTag>>().ConstructUsing(_ => new(Read()));
            builder.Map<Source, Destination<ResolveUsingTag>>().ResolveUsing((_, previous) => new(Read()));
            builder.Map<Source, Destination<ConvertTag>>().Convert(_ => new(Read()));
            builder.Map<Source, Destination<QualifiedConstructTag>>().Construct(_ => new(this.Read()));
            builder.Map<Source, Destination<QualifiedResolveTag>>().Resolve((_, previous) => new(this.Read()));
            builder.Map<Source, MemberDestination<QualifiedMembersTag>>().Members(_ => new() { Text = this.Read() });
            builder.Map<Source, Destination<QualifiedConstructUsingTag>>().ConstructUsing(_ => new(this.Read()));
            builder.Map<Source, Destination<QualifiedResolveUsingTag>>().ResolveUsing((_, previous) => new(this.Read()));
            builder.Map<Source, Destination<QualifiedConvertTag>>().Convert(_ => new(this.Read()));
            builder.Map<Source, Destination<ControlTag>>().Convert(_ => new(
                Field + ":" + this.Property + ":" + PublicField + ":" + this.PublicProperty + ":" +
                ReadStatic() + ":" + ReadVirtual() + ":" + HiddenVirtual() + ":" +
                Overload("text") + ":" + Generic<TMapper>((TMapper)this) + ":" + this.Generic<TMapper>((TMapper)this)));
            builder.Map<Source, Destination<ConvertGroupTag>>().Convert(ReadConverted);
            builder.Map<Source, Destination<FactoryGroupTag>>().ConstructUsing(this.ReadConstructed);
            builder.Map<Source, Destination<ResolverGroupTag>>().ResolveUsing(ReadResolved);
            builder.Map<Source, Destination<DelegateTag>>().Convert(Converter);
            builder.Map<Source, DeferredDestination>().ConstructUsing(_ => new(() => Read()));
            builder.Map<Source, Destination<CovariantTag>>().Convert(_ => new(
                Describe(ReadPayload()) + ":" + Describe(this.Payload)));
            builder.Map<Source, Destination<ThisTag>>().Convert(_ => new(DescribeMapper(this)));
            builder.Map<Source, Destination<InferredGenericTag>>().Convert(_ => new(
                Identify((TMapper)this) + ":" + this.Identify((TMapper)this)));
        }
    }

    public abstract class IntermediateMapper<TMapper> : BaseMapper<TMapper>
        where TMapper : IntermediateMapper<TMapper> { }

    [MorphantMapper]
    public partial class Mapper : IntermediateMapper<Mapper>
    {
        protected new string Read() => throw new InvalidOperationException("Hidden Read was selected.");
        protected new string Field = "derived-field";
        protected new string Property => "derived-property";
        public new string PublicField = "derived-public-field";
        public new string PublicProperty => "derived-public-property";
        protected new static string ReadStatic() => "derived-static";
        protected override string ReadVirtual() => "derived-virtual";
        public new virtual string HiddenVirtual() => "derived-virtual-slot";
        protected string Overload(string value) => "derived-string";
        protected new string Generic<TValue>(TValue value) => "derived-generic";
        protected new Destination<ConvertGroupTag> ReadConverted(Source? source) => throw new InvalidOperationException("Hidden Convert was selected.");
        protected new Destination<FactoryGroupTag> ReadConstructed(Source source) => throw new InvalidOperationException("Hidden factory was selected.");
        protected new Destination<ResolverGroupTag> ReadResolved(Source source, Option<Destination<ResolverGroupTag>> previous) => throw new InvalidOperationException("Hidden resolver was selected.");
        protected new Morphant.Delegates.Convert<Source?, Destination<DelegateTag>> Converter => throw new InvalidOperationException("Hidden delegate was selected.");
        protected override DerivedPayload ReadPayload() => new();
        protected override DerivedPayload Payload => new();

        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination<ConstructTag>>().IncludeBase<Source, Destination<ConstructTag>>();
            builder.Map<Source, Destination<ResolveTag>>().IncludeBase<Source, Destination<ResolveTag>>();
            builder.Map<Source, MemberDestination<MembersTag>>().IncludeBase<Source, MemberDestination<MembersTag>>();
            builder.Map<Source, Destination<ConstructUsingTag>>().IncludeBase<Source, Destination<ConstructUsingTag>>();
            builder.Map<Source, Destination<ResolveUsingTag>>().IncludeBase<Source, Destination<ResolveUsingTag>>();
            builder.Map<Source, Destination<ConvertTag>>().IncludeBase<Source, Destination<ConvertTag>>();
            builder.Map<Source, Destination<QualifiedConstructTag>>().IncludeBase<Source, Destination<QualifiedConstructTag>>();
            builder.Map<Source, Destination<QualifiedResolveTag>>().IncludeBase<Source, Destination<QualifiedResolveTag>>();
            builder.Map<Source, MemberDestination<QualifiedMembersTag>>().IncludeBase<Source, MemberDestination<QualifiedMembersTag>>();
            builder.Map<Source, Destination<QualifiedConstructUsingTag>>().IncludeBase<Source, Destination<QualifiedConstructUsingTag>>();
            builder.Map<Source, Destination<QualifiedResolveUsingTag>>().IncludeBase<Source, Destination<QualifiedResolveUsingTag>>();
            builder.Map<Source, Destination<QualifiedConvertTag>>().IncludeBase<Source, Destination<QualifiedConvertTag>>();
            builder.Map<Source, Destination<ControlTag>>().IncludeBase<Source, Destination<ControlTag>>();
            builder.Map<Source, Destination<ConvertGroupTag>>().IncludeBase<Source, Destination<ConvertGroupTag>>();
            builder.Map<Source, Destination<FactoryGroupTag>>().IncludeBase<Source, Destination<FactoryGroupTag>>();
            builder.Map<Source, Destination<ResolverGroupTag>>().IncludeBase<Source, Destination<ResolverGroupTag>>();
            builder.Map<Source, Destination<DelegateTag>>().IncludeBase<Source, Destination<DelegateTag>>();
            builder.Map<Source, DeferredDestination>().IncludeBase<Source, DeferredDestination>();
            builder.Map<Source, Destination<CovariantTag>>().IncludeBase<Source, Destination<CovariantTag>>();
            builder.Map<Source, Destination<ThisTag>>().IncludeBase<Source, Destination<ThisTag>>();
            builder.Map<Source, Destination<InferredGenericTag>>().IncludeBase<Source, Destination<InferredGenericTag>>();
        }
    }

    public static class Scenario
    {
        public static void VerifyFamily(CallbackKind callback, bool explicitThis)
        {
            var mapper = new Mapper();
            switch (callback)
            {
                case CallbackKind.Construct:
                    if (explicitThis) Verify((ITypeMapper<Source, Destination<QualifiedConstructTag>>)mapper, createsOnly: true);
                    else Verify((ITypeMapper<Source, Destination<ConstructTag>>)mapper, createsOnly: true);
                    break;
                case CallbackKind.Resolve:
                    if (explicitThis) Verify((ITypeMapper<Source, Destination<QualifiedResolveTag>>)mapper, createsOnly: false);
                    else Verify((ITypeMapper<Source, Destination<ResolveTag>>)mapper, createsOnly: false);
                    break;
                case CallbackKind.ConstructUsing:
                    if (explicitThis) Verify((ITypeMapper<Source, Destination<QualifiedConstructUsingTag>>)mapper, createsOnly: true);
                    else Verify((ITypeMapper<Source, Destination<ConstructUsingTag>>)mapper, createsOnly: true);
                    break;
                case CallbackKind.ResolveUsing:
                    if (explicitThis) Verify((ITypeMapper<Source, Destination<QualifiedResolveUsingTag>>)mapper, createsOnly: false);
                    else Verify((ITypeMapper<Source, Destination<ResolveUsingTag>>)mapper, createsOnly: false);
                    break;
                case CallbackKind.Convert:
                    if (explicitThis) Verify((ITypeMapper<Source, Destination<QualifiedConvertTag>>)mapper, createsOnly: false);
                    else Verify((ITypeMapper<Source, Destination<ConvertTag>>)mapper, createsOnly: false);
                    break;
                case CallbackKind.Members:
                    if (explicitThis) VerifyMembers((ITypeMapper<Source, MemberDestination<QualifiedMembersTag>>)mapper);
                    else VerifyMembers((ITypeMapper<Source, MemberDestination<MembersTag>>)mapper);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(callback));
            }
            Equal(callback is CallbackKind.Construct or CallbackKind.ConstructUsing ? 2 : 3,
                mapper.ReadCalls, "original method call count");
        }

        private static void Verify<TTag>(ITypeMapper<Source, Destination<TTag>> mapper, bool createsOnly)
        {
            Equal("base", mapper.Create(new Source()).Text, "Create binding");
            Equal("base", mapper.Update(new Source(), null).Text, "Update(null) binding");
            var previous = new Destination<TTag>("previous");
            var updated = mapper.Update(new Source(), previous);
            Equal(createsOnly ? "previous" : "base", updated.Text, "Update binding");
            Equal(createsOnly, ReferenceEquals(previous, updated), "Update identity");
        }

        private static void VerifyMembers<TTag>(ITypeMapper<Source, MemberDestination<TTag>> mapper)
        {
            Equal("base", mapper.Create(new Source()).Text, "Members Create");
            Equal("base", mapper.Update(new Source(), null).Text, "Members Update(null)");
            var previous = new MemberDestination<TTag> { Text = "previous" };
            var updated = mapper.Update(new Source(), previous);
            Equal("base", updated.Text, "Members Update");
            Equal(true, ReferenceEquals(previous, updated), "Members identity");
        }

        public static void VerifyOtherMembers()
        {
            var mapper = (ITypeMapper<Source, Destination<ControlTag>>)new Mapper();
            Equal("base-field:base-property:base-public-field:base-public-property:base-static:derived-virtual:base-virtual-slot:base-object:Mapper:Mapper",
                mapper.Create(new Source()).Text, "member selection and generic substitution");
        }

        public static void VerifyMethodGroups()
        {
            var mapper = new Mapper();
            Verify((ITypeMapper<Source, Destination<ConvertGroupTag>>)mapper, createsOnly: false);
            Verify((ITypeMapper<Source, Destination<FactoryGroupTag>>)mapper, createsOnly: true);
            Verify((ITypeMapper<Source, Destination<ResolverGroupTag>>)mapper, createsOnly: false);
            Verify((ITypeMapper<Source, Destination<DelegateTag>>)mapper, createsOnly: false);
            Equal(3, mapper.DelegateReads, "delegate getter once per invocation");
            Equal(11, mapper.ReadCalls, "original method groups");
        }

        public static void VerifyDeferredCapture()
        {
            var mapper = new Mapper();
            var result = ((ITypeMapper<Source, DeferredDestination>)mapper).Create(new Source());
            Equal(0, mapper.ReadCalls, "deferred callback remains deferred");
            Equal("base", result.Read(), "deferred original binding");
            Equal(1, mapper.ReadCalls, "deferred invocation count");
        }

        public static void VerifyCovariantResultTypes() => Equal("base-parameter:base-parameter",
            ((ITypeMapper<Source, Destination<CovariantTag>>)new Mapper()).Create(new Source()).Text,
            "covariant result retains the original overload selection");

        public static void VerifyThisType() => Equal("base-type",
            ((ITypeMapper<Source, Destination<ThisTag>>)new Mapper()).Create(new Source()).Text,
            "standalone this retains its declared type");

        public static void VerifyInferredGeneric() => Equal("Mapper:Mapper",
            ((ITypeMapper<Source, Destination<InferredGenericTag>>)new Mapper()).Create(new Source()).Text,
            "closing the family does not replace a generic overload");

        private static void Equal<T>(T expected, T actual, string operation)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"{operation}: expected {expected}, got {actual}.");
        }
    }
}
