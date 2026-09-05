using System;
using System.Diagnostics.CodeAnalysis;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Stage04Audit.Cases
{
    public sealed class Source
    {
        public int Id { get; set; }
        public int Initial { get; set; }
        public string? Text { get; set; }
    }

    public sealed class Destination
    {
        public Destination(int id) => Id = id * 10;
        public int Id { get; set; }
        public int Initial { get; init; }
        public string Text { get; set; } = "initial";
    }

    [MorphantMapper]
    public partial class ConventionMapper : TypeMapper<ConventionMapper>
    {
        protected override void Configure(MapperBuilder builder) => builder.Map<Source, Destination>();
    }

    [MorphantMapper]
    public partial class ResolveMapper : TypeMapper<ResolveMapper>
    {
        public static int InitialReads;
        private static int ReadInitial(Source source) { InitialReads++; return source.Initial; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Resolve((source, previous) =>
                {
                    if (previous.HasValue && previous.Value.Id == source.Id * 10)
                        return previous;
                    return new(source.Id);
                })
                .Members(source => new()
                {
                    Initial = ReadInitial(source),
                    Text = source.Text ?? "empty"
                });
    }

    [MorphantMapper]
    public partial class FactoryMapper : TypeMapper<FactoryMapper>
    {
        public static int FactoryCalls;
        public static int MemberCalls;
        public static MappingOperation LastOperation;

        private static Destination? Make(Source source, MappingContext context)
        {
            FactoryCalls++;
            LastOperation = context.Operation;
            return source.Id < 0 ? null : new Destination(source.Id) { Initial = 71 };
        }

        private static string ReadText(Source source) { MemberCalls++; return "mapped"; }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .NullSourceHandling(NullSourceHandling.ReturnDestination)
                .ConstructUsing((source, context) => Make(source, context)!)
                .Members(source => new() { Text = ReadText(source) });
    }

    public readonly struct ValueDestination
    {
        public ValueDestination(int id) => Id = id;
        public int Id { get; }
    }

    [MorphantMapper]
    public partial class ValueMapper : TypeMapper<ValueMapper>
    {
        public static bool LastPreviousHasValue;
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ValueDestination>();
            builder.Map<int, int?>()
                .ResolveUsing((source, previous) =>
                {
                    LastPreviousHasValue = previous.HasValue;
                    return previous.HasValue ? previous.Value : source;
                });
        }
    }

    public sealed record RecordDestination(int Id)
    {
        public string Text { get; init; } = "initial";
    }

    public sealed class GenericDestination<T>
    {
        public GenericDestination(T id) => Id = id;
        public T Id { get; }
    }

    public sealed class OptionalDestination
    {
        public OptionalDestination(decimal amount = 1.25m,
            DayOfWeek day = DayOfWeek.Friday, params string[] tags)
        {
            Amount = amount;
            Day = day;
            Tags = tags;
        }
        public decimal Amount { get; }
        public DayOfWeek Day { get; }
        public string[] Tags { get; }
    }

    [MorphantMapper]
    public partial class ShapeMapper : TypeMapper<ShapeMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, RecordDestination>().Members(source => new() { Text = "mapped" });
            builder.Map<Source, GenericDestination<int>>();
            builder.Map<Source, ValueDestination?>();
            builder.Map<Source, OptionalDestination>();
        }
    }

    public sealed class AttributeSource
    {
        [MaybeNull] public string Text => null;
        [NotNull] public string? Known => "known";
    }

    public sealed class AttributeDestination
    {
        private string _text;
        public AttributeDestination([AllowNull] string text) => _text = text ?? "accepted-null";
        [AllowNull] public string Text { get => _text; set => _text = value ?? "accepted-null"; }
        public string Known { get; set; } = "initial";
    }

    [MorphantMapper]
    public partial class AttributeMapper : TypeMapper<AttributeMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<AttributeSource, AttributeDestination>();
    }

    public interface IMutableDestination { int Id { get; set; } }
    public struct MutableDestination : IMutableDestination { public int Id { get; set; } }
    public abstract class AbstractDestination { public int Id { get; set; } }
    public sealed class ConcreteDestination : AbstractDestination { }

    [MorphantMapper]
    public partial class FactoryShapeMapper : TypeMapper<FactoryShapeMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, IMutableDestination>().ConstructUsing(source => new MutableDestination());
            builder.Map<Source, AbstractDestination>().ConstructUsing(source => new ConcreteDestination());
        }
    }

    [MorphantMapper]
    public partial class UpdateOnlyMapper : TypeMapper<UpdateOnlyMapper>
    {
        public static MappingOperation LastOperation;
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>(MappingMode.Update)
                .ConstructUsing((source, context) =>
                {
                    LastOperation = context.Operation;
                    return new Destination(source.Id);
                });
    }

    internal static class Scenario
    {
        internal static void Run()
        {
            var source = new Source { Id = 3, Initial = 4 };
            var convention = (ITypeMapper<Source, Destination>)new ConventionMapper();
            var created = convention.Create(source);
            Check.Equal("constructor-preserves-corresponding-member", 30, created.Id);
            Check.Equal("nullable-warning-convention-is-skipped", "initial", created.Text);
            var previous = new Destination(8) { Initial = 91 };
            var updated = convention.Update(source, previous);
            Check.Equal("convention-update-reuses-reference", true, ReferenceEquals(previous, updated));
            Check.Equal("convention-update-maps-constructor-member", 3, updated.Id);
            Check.Equal("convention-update-retains-init", 91, updated.Initial);

            var resolve = (ITypeMapper<Source, Destination>)new ResolveMapper();
            var replacement = resolve.Update(source, previous);
            Check.Equal("resolve-replacement-identity", false, ReferenceEquals(previous, replacement));
            Check.Equal("resolve-replacement-constructor", 30, replacement.Id);
            Check.Equal("resolve-replacement-init", 4, replacement.Initial);
            Check.Equal("resolve-replacement-init-count", 1, ResolveMapper.InitialReads);
            var reused = resolve.Update(source, replacement);
            Check.Equal("resolve-reuse-identity", true, ReferenceEquals(replacement, reused));
            Check.Equal("resolve-reuse-init-count", 1, ResolveMapper.InitialReads);
            Check.Equal("resolve-reuse-post-member", "empty", reused.Text);

            var factory = (ITypeMapper<Source, Destination>)new FactoryMapper();
            var produced = factory.Create(source);
            Check.Equal("factory-preserves-init", 71, produced.Initial);
            Check.Equal("factory-post-convention", 3, produced.Id);
            Check.Equal("factory-post-explicit", "mapped", produced.Text);
            var retained = factory.Update(source, produced);
            Check.Equal("construct-using-existing-reference", true, ReferenceEquals(produced, retained));
            Check.Equal("construct-using-existing-call-count", 1, FactoryMapper.FactoryCalls);
            var unavailable = factory.Update(source, null);
            Check.Equal("null-update-constructs", true, unavailable is not null);
            Check.Equal("null-update-operation", MappingOperation.Update, FactoryMapper.LastOperation);
            var callsBefore = FactoryMapper.MemberCalls;
            Check.Equal("factory-null-is-terminal", true, factory.Create(new Source { Id = -1 }) is null);
            Check.Equal("factory-null-skips-members", callsBefore, FactoryMapper.MemberCalls);
            Check.Equal("null-source-retains-destination", true, ReferenceEquals(produced, factory.Update(null, produced)));
            Check.Equal("null-source-skips-members", callsBefore, FactoryMapper.MemberCalls);

            var values = new ValueMapper();
            var value = (ITypeMapper<Source, ValueDestination>)values;
            Check.Equal("readonly-struct-create", 3, value.Create(source).Id);
            Check.Equal("readonly-struct-update-retains", 8, value.Update(source, new ValueDestination(8)).Id);
            var optionalValue = (ITypeMapper<int, int?>)values;
            Check.Equal<int?>("option-some-default-result", 0, optionalValue.Update(5, 0));
            Check.Equal("option-some-default-present", true, ValueMapper.LastPreviousHasValue);
            Check.Equal<int?>("option-null-result", 5, optionalValue.Update(5, null));
            Check.Equal("option-null-absent", false, ValueMapper.LastPreviousHasValue);

            var shapes = new ShapeMapper();
            Check.Equal("record-create-init", "mapped", ((ITypeMapper<Source, RecordDestination>)shapes).Create(source).Text);
            var recordPrevious = new RecordDestination(8);
            Check.Equal("record-update-retains-identity", true, ReferenceEquals(recordPrevious,
                ((ITypeMapper<Source, RecordDestination>)shapes).Update(source, recordPrevious)));
            Check.Equal("generic-constructor", 3, ((ITypeMapper<Source, GenericDestination<int>>)shapes).Create(source).Id);
            var nullableStruct = (ITypeMapper<Source, ValueDestination?>)shapes;
            Check.Equal("nullable-struct-null-update-creates", 3, nullableStruct.Update(source, null)!.Value.Id);
            Check.Equal("nullable-struct-default-is-existing", 0, nullableStruct.Update(source, default(ValueDestination))!.Value.Id);
            var optional = ((ITypeMapper<Source, OptionalDestination>)shapes).Create(source);
            Check.Equal("optional-decimal", 1.25m, optional.Amount);
            Check.Equal("optional-enum", DayOfWeek.Friday, optional.Day);
            Check.Equal("optional-params-empty", 0, optional.Tags.Length);

            var attribute = (ITypeMapper<AttributeSource, AttributeDestination>)new AttributeMapper();
            var attributes = attribute.Create(new AttributeSource());
            Check.Equal("allow-null-constructor", "accepted-null", attributes.Text);
            Check.Equal("not-null-source", "known", attributes.Known);
            Check.Equal("allow-null-setter", "accepted-null", attribute.Update(new AttributeSource(), new AttributeDestination("old")).Text);

            var factoryShapes = new FactoryShapeMapper();
            IMutableDestination boxed = new MutableDestination { Id = 8 };
            var boxedResult = ((ITypeMapper<Source, IMutableDestination>)factoryShapes).Update(source, boxed);
            Check.Equal("interface-retains-box", true, ReferenceEquals(boxed, boxedResult));
            Check.Equal("interface-mutates-box", 3, boxed.Id);
            var abstractResult = ((ITypeMapper<Source, AbstractDestination>)factoryShapes).Create(source);
            Check.Equal("abstract-factory-members", 3, abstractResult.Id);

            var updateOnly = (ITypeMapper<Source, Destination>)new UpdateOnlyMapper();
            Check.Equal("update-only-null-destination-creates", true, updateOnly.Update(source, null) is not null);
            Check.Equal("update-only-operation", MappingOperation.Update, UpdateOnlyMapper.LastOperation);
            Check.Throws<MappingOperationNotSupportedException>("update-only-rejects-create", () => updateOnly.Create(source));
        }
    }
}
