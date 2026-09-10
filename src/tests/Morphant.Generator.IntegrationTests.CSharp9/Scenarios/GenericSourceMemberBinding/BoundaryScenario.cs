#nullable enable
using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.GenericSourceMemberBinding
{
    public sealed class FieldTag { }
    public sealed class VirtualTag { }
    public sealed class MembersTag { }
    public sealed class IncludedTag { }
    public sealed class ConditionalIncludedTag { }
    public sealed class AssertedMembersTag { }
    public sealed class AssertedIncludedTag { }
    public sealed class ValueDestination<T> { public int? Value { get; set; } }

    public class BoundaryBase
    {
        public Profile Field = new() { Value = 12 };
        public virtual Profile Virtual => new() { Value = 21 };
        public Profile? OptionalValue;
        public int Reads;
        public Profile? Optional { get { Reads++; return OptionalValue; } }
    }
    public sealed class BoundarySource : BoundaryBase
    {
        public new Profile Field = new() { Value = 99 };
        public override Profile Virtual => new() { Value = 22 };
        public new Profile Optional => throw new InvalidOperationException("The hidden property was selected.");
    }
    public sealed class NullableBox<T> where T : BoundaryBase
    {
        public T? PayloadValue;
        public int Reads;
        public T? Payload { get { Reads++; return PayloadValue; } }
    }
    public sealed class BoundaryBox<T> where T : BoundaryBase
    {
        private T _payload = default!;
        public int Reads;
        public T Payload { get { Reads++; return _payload; } set { _payload = value; } }
    }

    public abstract class BoundaryFamily<TMapper, TSource> : TypeMapper<TMapper>
        where TMapper : BoundaryFamily<TMapper, TSource> where TSource : BoundaryBase
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<BoundaryBox<TSource>, ValueDestination<(FieldTag, MembersTag)>>()
                .Members(source => new() { Value = source.Payload.Field.Value });
            builder.Map<BoundaryBox<TSource>, ValueDestination<(FieldTag, IncludedTag)>>()
                .IncludeMembers(source => source.Payload.Field);
            builder.Map<BoundaryBox<TSource>, ValueDestination<(VirtualTag, MembersTag)>>()
                .Members(source => new() { Value = source.Payload.Virtual.Value });
            builder.Map<BoundaryBox<TSource>, ValueDestination<(VirtualTag, IncludedTag)>>()
                .IncludeMembers(source => source.Payload.Virtual);
            builder.Map<BoundaryBox<TSource>, ValueDestination<ConditionalIncludedTag>>()
                .IncludeMembers(source => source.Payload.Optional);
            builder.Map<NullableBox<TSource>, ValueDestination<MembersTag>>()
                .Members(source => new() { Value = source.Payload?.Optional?.Value });
            builder.Map<NullableBox<TSource>, ValueDestination<IncludedTag>>()
                .IncludeMembers(source => source.Payload?.Optional);
            builder.Map<NullableBox<TSource>, ValueDestination<AssertedMembersTag>>()
                .Members(source => new() { Value = source.Payload?.Optional!.Value });
            builder.Map<NullableBox<TSource>, ValueDestination<AssertedIncludedTag>>()
                .IncludeMembers(source => source.Payload?.Optional!);
        }
    }
    [MorphantMapper]
    public partial class BoundaryMapper : BoundaryFamily<BoundaryMapper, BoundarySource>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<BoundaryBox<BoundarySource>, ValueDestination<(FieldTag, MembersTag)>>()
                .IncludeBase<BoundaryBox<BoundarySource>, ValueDestination<(FieldTag, MembersTag)>>();
            builder.Map<BoundaryBox<BoundarySource>, ValueDestination<(FieldTag, IncludedTag)>>()
                .IncludeBase<BoundaryBox<BoundarySource>, ValueDestination<(FieldTag, IncludedTag)>>();
            builder.Map<BoundaryBox<BoundarySource>, ValueDestination<(VirtualTag, MembersTag)>>()
                .IncludeBase<BoundaryBox<BoundarySource>, ValueDestination<(VirtualTag, MembersTag)>>();
            builder.Map<BoundaryBox<BoundarySource>, ValueDestination<(VirtualTag, IncludedTag)>>()
                .IncludeBase<BoundaryBox<BoundarySource>, ValueDestination<(VirtualTag, IncludedTag)>>();
            builder.Map<BoundaryBox<BoundarySource>, ValueDestination<ConditionalIncludedTag>>()
                .IncludeBase<BoundaryBox<BoundarySource>, ValueDestination<ConditionalIncludedTag>>();
            builder.Map<NullableBox<BoundarySource>, ValueDestination<MembersTag>>()
                .IncludeBase<NullableBox<BoundarySource>, ValueDestination<MembersTag>>();
            builder.Map<NullableBox<BoundarySource>, ValueDestination<IncludedTag>>()
                .IncludeBase<NullableBox<BoundarySource>, ValueDestination<IncludedTag>>();
            builder.Map<NullableBox<BoundarySource>, ValueDestination<AssertedMembersTag>>()
                .IncludeBase<NullableBox<BoundarySource>, ValueDestination<AssertedMembersTag>>();
            builder.Map<NullableBox<BoundarySource>, ValueDestination<AssertedIncludedTag>>()
                .IncludeBase<NullableBox<BoundarySource>, ValueDestination<AssertedIncludedTag>>();
        }
    }

    public interface IProfileSource { Profile Profile { get; } }
    public struct MutableSource : IProfileSource
    {
        public int Reads;
        public Profile Profile { get { Reads++; return new() { Value = 31 }; } }
    }
    public sealed class ExplicitSource : IProfileSource
    {
        public int Reads;
        Profile IProfileSource.Profile { get { Reads++; return new() { Value = 41 }; } }
        public Profile Profile => throw new InvalidOperationException("The public property was selected.");
    }
    public sealed class InterfaceBox<T> where T : IProfileSource { public T Payload = default!; }
    public abstract class InterfaceFamily<TMapper, TSource> : TypeMapper<TMapper>
        where TMapper : InterfaceFamily<TMapper, TSource> where TSource : IProfileSource
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<InterfaceBox<TSource>, ValueDestination<MembersTag>>()
                .Members(source => new() { Value = source.Payload.Profile.Value });
            builder.Map<InterfaceBox<TSource>, ValueDestination<IncludedTag>>()
                .IncludeMembers(source => source.Payload.Profile);
        }
    }
    [MorphantMapper]
    public partial class StructMapper : InterfaceFamily<StructMapper, MutableSource>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<InterfaceBox<MutableSource>, ValueDestination<MembersTag>>()
                .IncludeBase<InterfaceBox<MutableSource>, ValueDestination<MembersTag>>();
            builder.Map<InterfaceBox<MutableSource>, ValueDestination<IncludedTag>>()
                .IncludeBase<InterfaceBox<MutableSource>, ValueDestination<IncludedTag>>();
        }
    }
    [MorphantMapper]
    public partial class InterfaceMapper : InterfaceFamily<InterfaceMapper, ExplicitSource>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<InterfaceBox<ExplicitSource>, ValueDestination<MembersTag>>()
                .IncludeBase<InterfaceBox<ExplicitSource>, ValueDestination<MembersTag>>();
            builder.Map<InterfaceBox<ExplicitSource>, ValueDestination<IncludedTag>>()
                .IncludeBase<InterfaceBox<ExplicitSource>, ValueDestination<IncludedTag>>();
        }
    }

    public static class BoundaryScenario
    {
        public static void VerifyConditionalMember(bool hasProfile, Operation operation)
        {
            var payload = new BoundarySource
            {
                OptionalValue = hasProfile ? new Profile { Value = 61 } : null
            };
            var source = new BoundaryBox<BoundarySource> { Payload = payload };
            VerifyValue<BoundaryBox<BoundarySource>, ConditionalIncludedTag>(
                new BoundaryMapper(), source, operation, hasProfile ? 61 : null);

            if (source.Reads != 1 || payload.Reads != 1)
                throw new InvalidOperationException("Conditional member access must evaluate each getter once.");
        }

        public static void VerifyFieldsAndVirtualDispatch(Callback callback, Operation operation)
        {
            var source = new BoundaryBox<BoundarySource> { Payload = new() };
            var mapper = new BoundaryMapper();
            if (callback == Callback.Members)
            {
                VerifyValue<BoundaryBox<BoundarySource>, (FieldTag, MembersTag)>(mapper, source, operation, 12);
                VerifyValue<BoundaryBox<BoundarySource>, (VirtualTag, MembersTag)>(mapper, source, operation, 22);
            }
            else
            {
                VerifyValue<BoundaryBox<BoundarySource>, (FieldTag, IncludedTag)>(mapper, source, operation, 12);
                VerifyValue<BoundaryBox<BoundarySource>, (VirtualTag, IncludedTag)>(mapper, source, operation, 22);
            }
        }

        public static void VerifyNullablePath(Callback callback, bool hasPayload, bool hasProfile, bool suppressNull)
        {
            var payload = hasPayload ? new BoundarySource { OptionalValue = hasProfile ? new Profile { Value = 51 } : null } : null;
            var source = new NullableBox<BoundarySource> { PayloadValue = payload };
            var mapper = new BoundaryMapper();
            bool threw = false;
            try
            {
                int? expected = hasPayload && hasProfile ? 51 : null;
                if (suppressNull && callback == Callback.Members)
                    VerifyValue<NullableBox<BoundarySource>, AssertedMembersTag>(mapper, source, Operation.Create, expected);
                else if (suppressNull)
                    VerifyValue<NullableBox<BoundarySource>, AssertedIncludedTag>(mapper, source, Operation.Create, expected);
                else if (callback == Callback.Members)
                    VerifyValue<NullableBox<BoundarySource>, MembersTag>(mapper, source, Operation.Create, expected);
                else
                    VerifyValue<NullableBox<BoundarySource>, IncludedTag>(mapper, source, Operation.Create, expected);
            }
            catch (NullReferenceException) { threw = true; }
            if (threw != (suppressNull && hasPayload && !hasProfile))
                throw new InvalidOperationException("Null propagation or the asserted path changed.");
            if (source.Reads != 1 || (payload is not null && payload.Reads != 1))
                throw new InvalidOperationException("A source getter was evaluated more than once.");
        }

        public static void VerifyInterfaceDispatch(Callback callback, bool valueType)
        {
            if (valueType)
            {
                var source = new InterfaceBox<MutableSource>();
                var mapper = new StructMapper();
                if (callback == Callback.Members)
                    VerifyValue<InterfaceBox<MutableSource>, MembersTag>(mapper, source, Operation.Create, 31);
                else
                    VerifyValue<InterfaceBox<MutableSource>, IncludedTag>(mapper, source, Operation.Create, 31);
                if (source.Payload.Reads != 1)
                    throw new InvalidOperationException("Interface access copied the mutable struct receiver.");
            }
            else
            {
                var source = new InterfaceBox<ExplicitSource> { Payload = new() };
                var mapper = new InterfaceMapper();
                if (callback == Callback.Members)
                    VerifyValue<InterfaceBox<ExplicitSource>, MembersTag>(mapper, source, Operation.Create, 41);
                else
                    VerifyValue<InterfaceBox<ExplicitSource>, IncludedTag>(mapper, source, Operation.Create, 41);
                if (source.Payload.Reads != 1)
                    throw new InvalidOperationException("The explicit interface getter must be called once.");
            }
        }

        private static void VerifyValue<TSource, TTag>(ITypeMapper<TSource, ValueDestination<TTag>> mapper,
            TSource source, Operation operation, int? expected)
        {
            var previous = operation == Operation.UpdateExisting ? new ValueDestination<TTag> { Value = 7 } : null;
            var result = operation == Operation.Create ? mapper.Create(source) : mapper.Update(source, previous);
            if (result.Value != expected)
                throw new InvalidOperationException($"Expected {expected}, got {result.Value}.");
            if (previous is not null && !ReferenceEquals(previous, result))
                throw new InvalidOperationException("Update replaced the destination.");
        }
    }
}
