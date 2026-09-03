// Compiled integration scenario: TypeMapperConventionTests/MemberTests::Does_not_use_mapper_lexical_access_to_private_destination_members
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.Member_a76b168f
{
    public sealed class Source
    {
        public int Visible { get; init; }

        public int Secret { get; init; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        public sealed class Destination
        {
            public int Visible { get; set; }

            public int Secret { get; private set; } = 71;
        }

        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, TestMapper.Destination>)
                new TestMapper();
            var source = new Source
            {
                Visible = 73,
                Secret = 79
            };
            var created = mapper.Create(source, default(MappingContext));
            var updated = mapper.Update(
                source,
                created,
                default(MappingContext));

            if (!ReferenceEquals(created, updated) ||
                created.Visible != 73 ||
                created.Secret != 71)
            {
                throw new InvalidOperationException(
                    "Mapper lexical access leaked into member conventions.");
            }
        }
    }
}
