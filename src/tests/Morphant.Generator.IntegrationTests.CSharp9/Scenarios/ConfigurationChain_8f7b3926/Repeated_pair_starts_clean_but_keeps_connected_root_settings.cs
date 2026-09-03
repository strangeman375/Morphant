// Compiled integration scenario: TypeMapperInheritanceTests/ConfigurationChainTests::Repeated_pair_starts_clean_but_keeps_connected_root_settings
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.ConfigurationChain_8f7b3926
{
    public sealed class Source
    {
        public string Name { get; init; } = string.Empty;

        public string Code { get; init; } = string.Empty;
    }

    public sealed class Destination
    {
        public string Name { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;
    }

    public abstract class BaseMapper : TypeMapper<BaseMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.MemberSelection(MemberSelection.Explicit);
            builder.Map<Source, Destination>()
                .NullSourceHandling(NullSourceHandling.Throw)
                .Members((source, _) => new()
                {
                    Name = "base:" + source.Name
                });
        }
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>()
                .Members((source, _) => new()
                {
                    Code = "derived:" + source.Code
                });
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new DerivedMapper();
            var result = mapper.Create(
                new Source { Name = "name", Code = "code" },
                default);

            if (result.Name != string.Empty ||
                result.Code != "derived:code" ||
                mapper.Create(null, default) is not null)
            {
                throw new InvalidOperationException(
                    "A clean local pair inherited pair-level state or lost base roots.");
            }
        }
    }
}
