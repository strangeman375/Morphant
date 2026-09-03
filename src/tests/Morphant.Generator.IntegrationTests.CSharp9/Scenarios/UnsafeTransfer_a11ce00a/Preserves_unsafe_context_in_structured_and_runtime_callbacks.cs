// Compiled integration scenario: TypeMapperCSharpSemanticsTests::Preserves_unsafe_context_in_structured_and_runtime_callbacks
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.UnsafeTransfer_a11ce00a
{
    public sealed class Source
    {
        public IntPtr Pointer { get; set; }
    }

    public sealed class ConstructDestination
    {
        public ConstructDestination(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class ResolveDestination
    {
        public ResolveDestination(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class MembersDestination
    {
        public int Value { get; set; }
    }

    public sealed class RuntimeDestination
    {
        public RuntimeDestination(int value) => Value = value;

        public int Value { get; }
    }

    public sealed class ConvertDestination
    {
        public ConvertDestination(int value) => Value = value;

        public int Value { get; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override unsafe void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConstructDestination>()
                .Construct(source => new(*(int*)source.Pointer));

            builder.Map<Source, ResolveDestination>()
                .Resolve((source, _) => new(*(int*)source.Pointer));

            builder.Map<Source, MembersDestination>()
                .Members(source => new()
                {
                    Value = *(int*)source.Pointer
                });

            builder.Map<Source, RuntimeDestination>()
                .ConstructUsing(source =>
                    new RuntimeDestination(*(int*)source.Pointer));

            builder.Map<Source, ConvertDestination>()
                .Convert(source =>
                    new ConvertDestination(*(int*)source!.Pointer));
        }
    }

    public static class Scenario
    {
        public static unsafe void Verify()
        {
            var value = 37;
            var source = new Source
            {
                Pointer = (IntPtr)(&value)
            };
            var mapper = new TestMapper();

            AssertValue<ConstructDestination>(
                mapper,
                source,
                static destination => destination.Value);
            AssertValue<ResolveDestination>(
                mapper,
                source,
                static destination => destination.Value);
            AssertValue<MembersDestination>(
                mapper,
                source,
                static destination => destination.Value);
            AssertValue<RuntimeDestination>(
                mapper,
                source,
                static destination => destination.Value);
            AssertValue<ConvertDestination>(
                mapper,
                source,
                static destination => destination.Value);
        }

        private static void AssertValue<TDestination>(
            TestMapper mapper,
            Source source,
            Func<TDestination, int> read)
        {
            var destination =
                ((ITypeMapper<Source, TDestination>)mapper)
                .Create(source, default(MappingContext));

            if (read(destination) != 37)
            {
                throw new InvalidOperationException(
                    "Unsafe callback code changed semantics.");
            }
        }
    }
}
