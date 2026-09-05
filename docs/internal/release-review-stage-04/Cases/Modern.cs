using System.Diagnostics.CodeAnalysis;
using Morphant;

namespace Stage04Audit.Cases
{
    public sealed class Source
    {
        public int Id { get; set; }
        public int Initial { get; set; }
        public int Mutable { get; set; }
    }

    public class RequiredBase { public required int Initial { get; init; } }
    public sealed class RequiredDestination : RequiredBase
    {
        public RequiredDestination(int id) => Id = id;
        public int Id { get; }
        public required int Mutable;
    }

    public sealed class AnnotatedDestination
    {
        [SetsRequiredMembers]
        public AnnotatedDestination(int id) => Id = id + 100;
        public required int Id { get; init; }
    }

    public struct ExplicitStruct
    {
        public ExplicitStruct() { Stamp = 73; Id = 0; }
        public int Stamp { get; set; }
        public int Id { get; set; }
    }

    public readonly record struct ReadonlyRecordDestination(int Id);

    [MorphantMapper]
    public partial class Mapper : TypeMapper<Mapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, RequiredDestination>();
            builder.Map<Source, AnnotatedDestination>();
            builder.Map<Source, ExplicitStruct>();
            builder.Map<Source, ReadonlyRecordDestination>();
        }
    }

    internal static class Scenario
    {
        internal static void Run()
        {
            var mapper = new Mapper();
            var source = new Source { Id = 11, Initial = 13, Mutable = 17 };
            var required = (ITypeMapper<Source, RequiredDestination>)mapper;
            var created = required.Create(source);
            Check.Equal("inherited-required-init-create", 13, created.Initial);
            Check.Equal("required-field-create", 17, created.Mutable);
            var previous = new RequiredDestination(19) { Initial = 23, Mutable = 29 };
            var updated = required.Update(source, previous);
            Check.Equal("inherited-required-init-update-retains", 23, updated.Initial);
            Check.Equal("required-field-update", 17, updated.Mutable);
            Check.Equal("constructor-only-update-retains", 19, updated.Id);
            var annotated = ((ITypeMapper<Source, AnnotatedDestination>)mapper).Create(source);
            Check.Equal("sets-required-constructor-result", 111, annotated.Id);
            var value = (ITypeMapper<Source, ExplicitStruct>)mapper;
            var fresh = value.Create(source);
            Check.Equal("explicit-struct-constructor", 73, fresh.Stamp);
            Check.Equal("explicit-struct-constructor-members", 11, fresh.Id);
            Check.Equal("null-source-zero-initializes-struct", 0, value.Create(null).Stamp);
            Check.Equal("struct-update-retains-default-stamp", 0, value.Update(source, default).Stamp);
            var record = (ITypeMapper<Source, ReadonlyRecordDestination>)mapper;
            Check.Equal("readonly-record-struct-create", 11, record.Create(source).Id);
            Check.Equal("readonly-record-struct-update", 19, record.Update(source, new ReadonlyRecordDestination(19)).Id);
        }
    }
}
