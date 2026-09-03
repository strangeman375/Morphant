// Compiled integration scenario: TypeMapperStructuredConstructTests/ExplicitConstructorTests::Preserves_overload_argument_order_casts_and_omission
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using Morphant.Members;
using System;
using System.Collections.Generic;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ExplicitConstructor_3e4adc35
{
    public sealed class Source
    {
        public long Id { get; init; }

        public string Label { get; init; } = string.Empty;
    }

    public sealed class OverloadedDestination
    {
        public OverloadedDestination(int id, string label)
        {
            Kind = "int";
            Id = id;
            Label = label;
        }

        public OverloadedDestination(long id, string label)
        {
            Kind = "long";
            Id = id;
            Label = label;
        }

        public string Kind { get; }

        public long Id { get; }

        public string Label { get; }
    }

    public sealed class OptionalDestination
    {
        public OptionalDestination(
            long id,
            bool enabled = true,
            params string[] tags)
        {
            Id = id;
            Enabled = enabled;
            Tags = tags;
        }

        public long Id { get; }

        public bool Enabled { get; }

        public string[] Tags { get; }
    }

    public sealed class ParamsDestination
    {
        public ParamsDestination(
            long id,
            bool enabled = true,
            params string[] tags)
        {
            Id = id;
            Enabled = enabled;
            Tags = tags;
        }

        public long Id { get; }

        public bool Enabled { get; }

        public string[] Tags { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public static List<string> Events { get; } = new();

        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, OverloadedDestination>()
                .Construct(source => new(
                    label: Track("label", source.Label),
                    id: (ConstructorParameter<long>)Track("id", source.Id)));

            builder.Map<Source, OptionalDestination>()
                .Construct(_ => new(
                    Auto(),
                    Ignore(),
                    Ignore()));

            builder.Map<Source, ParamsDestination>()
                .Construct(source => new(
                    Track("params-id", source.Id),
                    tags: Track(
                        "tags",
                        new[] { source.Label })));
        }

        private static T Track<T>(string name, T value)
        {
            Events.Add(name);
            return value;
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source
            {
                Id = 17,
                Label = "selected"
            };
            var context = default(MappingContext);
            var overloaded =
                ((ITypeMapper<Source, OverloadedDestination>)mapper)
                .Create(source, context);
            var optional =
                ((ITypeMapper<Source, OptionalDestination>)mapper)
                .Create(source, context);
            var withParams =
                ((ITypeMapper<Source, ParamsDestination>)mapper)
                .Create(source, context);

            if (overloaded.Kind != "long" ||
                overloaded.Id != 17 ||
                overloaded.Label != "selected" ||
                TestMapper.Events.Count != 4 ||
                TestMapper.Events[0] != "label" ||
                TestMapper.Events[1] != "id" ||
                TestMapper.Events[2] != "params-id" ||
                TestMapper.Events[3] != "tags" ||
                optional.Id != 17 ||
                !optional.Enabled ||
                optional.Tags.Length != 0 ||
                withParams.Id != 17 ||
                !withParams.Enabled ||
                withParams.Tags.Length != 1 ||
                withParams.Tags[0] != "selected")
            {
                throw new InvalidOperationException(
                    "Explicit constructor lowering changed C# binding or evaluation.");
            }
        }
    }
}
