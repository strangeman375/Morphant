// Compiled integration scenario: TypeMapperRuntimeConstructionTests/CaptureTests::Rejects_runtime_Configure_locals_for_runtime_callbacks
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0030

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Capture_0ca4831f
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public Destination(int value)
        {
            Value = value;
        }

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            var offset = Environment.TickCount;

            builder.Map<Source, int>()
                .ConstructUsing(source => source.Value + offset);

            builder.Map<Source, Destination>()
                .ConstructUsing(source =>
                    new Destination(source.Value + offset));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 3 };
            var context = default(MappingContext);

            AssertUnsupported(() =>
                ((ITypeMapper<Source, int>)mapper)
                .Create(source, context));
            AssertUnsupported(() =>
                ((ITypeMapper<Source, Destination>)mapper)
                .Create(source, context));
        }

        private static void AssertUnsupported(Action action)
        {
            try
            {
                action();
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A Configure-local capture escaped into generated code.");
        }
    }
}
