// Compiled integration scenario: TypeMapperConventionTests/MemberTests::Matches_members_by_exact_case_sensitive_name_only
#nullable enable
#pragma warning disable CS1591

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MemberNames_9d7a0302
{
    public sealed class Source
    {
        public int Exact { get; init; }

        public int caseOnly { get; init; }

        public int Value { get; init; }

        public int value { get; init; }
    }

    public sealed class Destination
    {
        public int Exact { get; set; } = -1;

        public int CaseOnly { get; set; } = -2;

        public int Value { get; set; } = -3;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper =
                (ITypeMapper<Source, Destination>)new TestMapper();
            var source = new Source
            {
                Exact = 11,
                caseOnly = 13,
                Value = 17,
                value = 19
            };
            var created = mapper.Create(source, default(MappingContext));
            var previous = new Destination
            {
                Exact = 23,
                CaseOnly = 29,
                Value = 31
            };
            var updated = mapper.Update(
                source,
                previous,
                default(MappingContext));

            if (created.Exact != 11 ||
                created.CaseOnly != -2 ||
                created.Value != 17 ||
                !ReferenceEquals(updated, previous) ||
                updated.Exact != 11 ||
                updated.CaseOnly != 29 ||
                updated.Value != 17)
            {
                throw new InvalidOperationException(
                    "Member convention used a case-insensitive candidate or " +
                    "failed to prefer the exact case-sensitive member.");
            }
        }
    }
}
