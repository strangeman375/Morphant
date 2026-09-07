#nullable enable
#pragma warning disable CS1591

using System;
using System.Diagnostics.CodeAnalysis;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.RequiredConstructorMembers_s0501
{
    public enum ConstructionRoute { Automatic, ByConvention, Explicit }

    public sealed class Source
    {
        public int Reads { get; private set; }
        public int Value { get { Reads++; return 7; } }
    }

    public interface IObservedDestination
    {
        int Value { get; }
        int ConstructorValue { get; }
        int Writes { get; }
    }

    public sealed class RequiredInit : IObservedDestination
    {
        private int value;
        public RequiredInit(int value) { ConstructorValue = value; Value = value; }
        public int ConstructorValue { get; }
        public int Writes { get; private set; }
        public required int Value { get => value; init { this.value = value; Writes++; } }
    }

    public sealed class RequiredSet : IObservedDestination
    {
        private int value;
        public RequiredSet(int value) { ConstructorValue = value; Value = value; }
        public int ConstructorValue { get; }
        public int Writes { get; private set; }
        public required int Value { get => value; set { this.value = value; Writes++; } }
    }

    public sealed class AttributedInit : IObservedDestination
    {
        private int value;
        [SetsRequiredMembers]
        public AttributedInit(int value) { ConstructorValue = value; Value = value + 100; }
        public int ConstructorValue { get; }
        public int Writes { get; private set; }
        public required int Value { get => value; init { this.value = value; Writes++; } }
    }

    public sealed class AttributedSet : IObservedDestination
    {
        private int value;
        [SetsRequiredMembers]
        public AttributedSet(int value) { ConstructorValue = value; Value = value + 100; }
        public int ConstructorValue { get; }
        public int Writes { get; private set; }
        public required int Value { get => value; set { this.value = value; Writes++; } }
    }

    [MorphantMapper]
    public partial class AutomaticMapper : TypeMapper<AutomaticMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, RequiredInit>().Members(source => new() { Value = source.Value + 10 });
            builder.Map<Source, RequiredSet>().Members(source => new() { Value = source.Value + 10 });
            builder.Map<Source, AttributedInit>().Members(source => new() { Value = source.Value + 10 });
            builder.Map<Source, AttributedSet>().Members(source => new() { Value = source.Value + 10 });
        }
    }

    [MorphantMapper]
    public partial class ConventionMapper : TypeMapper<ConventionMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, RequiredInit>().Construct(_ => new(ByConvention()))
                .Members(source => new() { Value = source.Value + 10 });
            builder.Map<Source, RequiredSet>().Construct(_ => new(ByConvention()))
                .Members(source => new() { Value = source.Value + 10 });
            builder.Map<Source, AttributedInit>().Construct(_ => new(ByConvention()))
                .Members(source => new() { Value = source.Value + 10 });
            builder.Map<Source, AttributedSet>().Construct(_ => new(ByConvention()))
                .Members(source => new() { Value = source.Value + 10 });
        }
    }

    [MorphantMapper]
    public partial class ExplicitMapper : TypeMapper<ExplicitMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, RequiredInit>().Construct(source => new(source.Value))
                .Members(source => new() { Value = source.Value + 10 });
            builder.Map<Source, RequiredSet>().Construct(source => new(source.Value))
                .Members(source => new() { Value = source.Value + 10 });
            builder.Map<Source, AttributedInit>().Construct(source => new(source.Value))
                .Members(source => new() { Value = source.Value + 10 });
            builder.Map<Source, AttributedSet>().Construct(source => new(source.Value))
                .Members(source => new() { Value = source.Value + 10 });
        }
    }

    [MorphantMapper]
    public partial class AutomaticControlMapper : TypeMapper<AutomaticControlMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, RequiredInit>();
            builder.Map<Source, AttributedInit>();
        }
    }

    public static class Scenario
    {
        public static void VerifyRequiredInit(ConstructionRoute route) => Verify(
            (ITypeMapper<Source, RequiredInit>)CreateMapper(route),
            new RequiredInit(99) { Value = 98 }, route, creationOnly: true);

        public static void VerifyRequiredSet(ConstructionRoute route) => Verify(
            (ITypeMapper<Source, RequiredSet>)CreateMapper(route),
            new RequiredSet(99) { Value = 98 }, route, creationOnly: false);

        public static void VerifyAttributedInit(ConstructionRoute route) => Verify(
            (ITypeMapper<Source, AttributedInit>)CreateMapper(route),
            new AttributedInit(99) { Value = 98 }, route, creationOnly: true);

        public static void VerifyAttributedSet(ConstructionRoute route) => Verify(
            (ITypeMapper<Source, AttributedSet>)CreateMapper(route),
            new AttributedSet(99) { Value = 98 }, route, creationOnly: false);

        private static void Verify<TDestination>(ITypeMapper<Source, TDestination> mapper,
            TDestination previous, ConstructionRoute route, bool creationOnly)
            where TDestination : class, IObservedDestination
        {
            var source = new Source();
            int expectedReads = 1;
            VerifyCreated(mapper.Create(source), source, expectedReads);
            source = new Source();
            VerifyCreated(mapper.Update(source, null), source, expectedReads);
            source = new Source();
            var updated = mapper.Update(source, previous);
            Equal(true, ReferenceEquals(previous, updated), "Update identity");
            Equal(99, updated.ConstructorValue, "Update constructor value");
            Equal(creationOnly ? 2 : 3, updated.Writes, "Update writes only mutable members");
            Equal(creationOnly ? 98 : 17, updated.Value, "Update member value");
            Equal(creationOnly ? 0 : 1, source.Reads, "Update member reads");
        }

        private static void VerifyCreated(IObservedDestination created, Source source, int expectedReads)
        {
            bool attributed = created is AttributedInit or AttributedSet;
            Equal(17, created.ConstructorValue, "constructor receives the member value");
            Equal(attributed ? 117 : 17, created.Value, "constructor normalization and required initializer");
            Equal(attributed ? 1 : 2, created.Writes, "only the required initializer repeats assignment");
            Equal(expectedReads, source.Reads, "constructor and member dependency reads");
        }

        public static void VerifyAutomaticControls()
        {
            var mapper = new AutomaticControlMapper();
            var source = new Source();
            var required = ((ITypeMapper<Source, RequiredInit>)mapper).Create(source);
            Equal(7, required.ConstructorValue, "required convention constructor");
            Equal(7, required.Value, "required convention initializer");
            Equal(1, source.Reads, "automatic required value shared once");
            source = new Source();
            var attributed = ((ITypeMapper<Source, AttributedInit>)mapper).Create(source);
            Equal(107, attributed.Value, "SetsRequiredMembers convention value preserved");
            Equal(1, source.Reads, "no redundant required convention read");
        }

        private static object CreateMapper(ConstructionRoute route) => route switch
        {
            ConstructionRoute.Automatic => new AutomaticMapper(),
            ConstructionRoute.ByConvention => new ConventionMapper(),
            ConstructionRoute.Explicit => new ExplicitMapper(),
            _ => throw new ArgumentOutOfRangeException(nameof(route))
        };

        private static void Equal<T>(T expected, T actual, string operation)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"{operation}: expected {expected}, got {actual}.");
        }
    }
}
