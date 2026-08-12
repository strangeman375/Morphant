// Compiled integration scenario: TypeMapperExpressionTransferTests::Rejects_file_local_helpers_in_all_structured_surfaces
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0030

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.FileLocal_a11ce006
{
    public sealed class Source
    {
        public int Value { get; init; }
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

    file static class HiddenHelper
    {
        public static int Read(int value) => value;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<Source, ConstructDestination>()
                .Construct(source => new(
                    HiddenHelper.Read(source.Value)));

            builder.Map<Source, ResolveDestination>()
                .Resolve((source, _) => new(
                    HiddenHelper.Read(source.Value)));

            builder.Map<Source, MembersDestination>()
                .Members(source => new()
                {
                    Value = HiddenHelper.Read(source.Value)
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var source = new Source { Value = 7 };

            ExpectUnsupported<ConstructDestination>(mapper, source);
            ExpectUnsupported<ResolveDestination>(mapper, source);
            ExpectUnsupported<MembersDestination>(mapper, source);
        }

        private static void ExpectUnsupported<TDestination>(
            TestMapper mapper,
            Source source)
        {
            try
            {
                ((ITypeMapper<Source, TDestination>)mapper).Create(
                    source,
                    default(MappingContext));
            }
            catch (global::Morphant.Exceptions.MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A file-local helper escaped into generated code.");
        }
    }
}
