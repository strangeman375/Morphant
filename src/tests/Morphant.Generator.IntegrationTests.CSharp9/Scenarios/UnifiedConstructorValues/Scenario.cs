#nullable enable
#pragma warning disable CS1591
using System;
using Morphant;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnifiedConstructorValues
{
    public enum Route { Automatic, ByConvention, Auto, Value, ByConventionAuto, ByConventionValue, Resolve, Omitted }
    public sealed class Source
    {
        public int Calls { get; private set; }
        public int Read() { Calls++; return 117; }
        public int ConstructorCalls { get; private set; }
        public int ConstructValue() { ConstructorCalls++; return 23; }
    }
    public sealed class Destination
    {
        private int value;
        public Destination(int value = -1)
        {
            ConstructorValue = value;
            Value = value + 1000;
        }
        public int ConstructorValue { get; }
        public int Writes { get; private set; }
        public int Value { get => value; set { this.value = value; Writes++; } }
    }
    [MorphantMapper]
    public partial class AutomaticMapper : TypeMapper<AutomaticMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .Members(source => new() { Value = source.Read() });
    }
    [MorphantMapper]
    public partial class ByConventionMapper : TypeMapper<ByConventionMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Construct(_ => new(ByConvention()))
                .Members(source => new() { Value = source.Read() });
    }
    [MorphantMapper]
    public partial class AutoMapper : TypeMapper<AutoMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Construct(_ => new(Auto()))
                .Members(source => new() { Value = source.Read() });
    }
    [MorphantMapper]
    public partial class ValueMapper : TypeMapper<ValueMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Construct(source => new(source.ConstructValue()))
                .Members(source => new() { Value = source.Read() });
    }
    [MorphantMapper]
    public partial class ByConventionAutoMapper : TypeMapper<ByConventionAutoMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Construct(_ => new(ByConvention(), new() { value = Auto() }))
                .Members(source => new() { Value = source.Read() });
    }
    [MorphantMapper]
    public partial class ByConventionValueMapper : TypeMapper<ByConventionValueMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Construct(source => new(ByConvention(), new() { value = source.ConstructValue() }))
                .Members(source => new() { Value = source.Read() });
    }
    [MorphantMapper]
    public partial class ResolveMapper : TypeMapper<ResolveMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Resolve((_, previous) => { if (previous.HasValue) return previous.Value; return new(Auto()); })
                .Members(source => new() { Value = source.Read() });
    }
    [MorphantMapper]
    public partial class OmittedMapper : TypeMapper<OmittedMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>().Construct(_ => new())
                .Members(source => new() { Value = source.Read() });
    }
    public sealed class AutoSource
    {
        public int Reads { get; private set; }
        public int Value { get { Reads++; return 117; } }
        public int ConstructValue() => 23;
    }
    [MorphantMapper]
    public partial class MemberAutoMapper : TypeMapper<MemberAutoMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<AutoSource, Destination>()
                .Construct(source => new(source.ConstructValue()))
                .Members(_ => new() { Value = Auto() });
    }
    public sealed class TextSource
    {
        public int Calls { get; private set; }
        public string Read() { Calls++; return "member"; }
        public object ConstructValue() => "constructor";
    }
    public sealed class OverloadedDestination
    {
        private string value = "";
        public OverloadedDestination(object value) { ObjectOverload = true; Value = (string)value; }
        public OverloadedDestination(string value) { Value = value; }
        public bool ObjectOverload { get; }
        public int Writes { get; private set; }
        public string Value { get => value; set { this.value = value; Writes++; } }
    }
    [MorphantMapper]
    public partial class OverloadMapper : TypeMapper<OverloadMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<TextSource, OverloadedDestination>()
                .Construct(source => new(source.ConstructValue()))
                .Members(source => new() { Value = source.Read() });
    }
    public static class Scenario
    {
        public static void Verify(Route route)
        {
            ITypeMapper<Source, Destination> mapper = route switch
            {
                Route.Automatic => new AutomaticMapper(),
                Route.ByConvention => new ByConventionMapper(),
                Route.Auto => new AutoMapper(),
                Route.Value => new ValueMapper(),
                Route.ByConventionAuto => new ByConventionAutoMapper(),
                Route.ByConventionValue => new ByConventionValueMapper(),
                Route.Resolve => new ResolveMapper(),
                Route.Omitted => new OmittedMapper(),
                _ => throw new ArgumentOutOfRangeException(nameof(route))
            };
            var source = new Source();
            var explicitValue = route is Route.Value or Route.ByConventionValue;
            var independent = explicitValue || route == Route.Omitted;
            var expectedArgument = explicitValue ? 23 : route == Route.Omitted ? -1 : 117;
            var expectedValue = independent ? 117 : 1117;
            var expectedWrites = independent ? 2 : 1;
            var created = mapper.Create(source);
            Check(created.Value == expectedValue && created.ConstructorValue == expectedArgument && created.Writes == expectedWrites,
                "Create must preserve each explicit rule and share only automatic constructor values.");
            Check(source.Calls == 1 && source.ConstructorCalls == (explicitValue ? 1 : 0),
                "Create evaluated an explicit rule an incorrect number of times.");
            var replacement = mapper.Update(source, null);
            Check(replacement.Value == expectedValue && replacement.Writes == expectedWrites && source.Calls == 2,
                "Update without a destination must use the same creation rule.");
            var updated = mapper.Update(source, created);
            Check(ReferenceEquals(created, updated) && updated.Value == 117 && updated.Writes == expectedWrites + 1,
                "Update with a destination must apply the value through its setter.");
            Check(source.Calls == 3 && source.ConstructorCalls == (explicitValue ? 2 : 0),
                "Reuse must skip construction and evaluate the member rule once.");
        }
        public static void VerifyMemberAuto()
        {
            ITypeMapper<AutoSource, Destination> mapper = new MemberAutoMapper();
            var source = new AutoSource();
            var created = mapper.Create(source);
            Check(source.Reads == 1 && created.ConstructorValue == 23 && created.Value == 117 && created.Writes == 2,
                "Explicit member Auto must run after the explicit constructor value.");
        }
        public static void VerifyOverload()
        {
            ITypeMapper<TextSource, OverloadedDestination> mapper = new OverloadMapper();
            var source = new TextSource();
            var created = mapper.Create(source);
            Check(created.ObjectOverload && created.Value == "member" && created.Writes == 2 && source.Calls == 1,
                "Independent member assignment must preserve the explicitly selected object overload.");
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
