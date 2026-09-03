// Compiled integration scenario: TypeMapperCSharpSemanticsTests::Preserves_async_runtime_callbacks
#nullable enable
#pragma warning disable CS1591

using System;
using System.Threading.Tasks;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.AsyncTransfer_a11ce009
{
    public sealed class Source
    {
        public int Value { get; set; }

        public IntPtr Pointer { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, int>()
                .ConstructUsing(source =>
                {
                    unsafe
                    {
                        return *(int*)source.Pointer;
                    }
                });

            builder.Map<Source, Task<int>>()
                .ConstructUsing(static async source =>
                {
                    await Task.Yield();
                    return source.Value + 1;
                });

            builder.Map<Source, Task<string>>()
                .ResolveUsing(async (source, previous) =>
                {
                    await Task.Yield();
                    return source.Value + ":" + previous.HasValue;
                });

            builder.Map<Source, Task<long>>()
                .Convert(async (source, previous, context) =>
                {
                    await Task.Yield();
                    return source!.Value +
                        (previous.HasValue ? 10L : 0L) +
                        (context.Operation == MappingOperation.Create
                            ? 100L
                            : 0L);
                });
        }
    }

    public static class Scenario
    {
        public static async Task Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 7 };

            var construct = await
                ((ITypeMapper<Source, Task<int>>)mapper)
                .Create(source);
            var resolve = await
                ((ITypeMapper<Source, Task<string>>)mapper)
                .Create(source);
            var convert = await
                ((ITypeMapper<Source, Task<long>>)mapper)
                .Create(source);

            if (construct != 8 ||
                resolve != "7:False" ||
                convert != 107L)
            {
                throw new InvalidOperationException(
                    "An async runtime callback changed semantics.");
            }
        }
    }
}
