// Compiled integration scenario: MappingCompletenessDiagnosticsTests::Suppressed_warnings_do_not_change_create_or_update_behavior
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0047, MORPH0048

using System;
using Morphant;
using Morphant.Context;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.MappingCompleteness_a11ce011
{
    public sealed class Source
    {
        private int _legacyReads;

        public int Used { get; set; }

        public int Unused { get; set; }

        public int Legacy
        {
            get
            {
                _legacyReads++;
                return 41;
            }
        }

        public int GetLegacyReads() => _legacyReads;
    }

    public sealed class Destination
    {
        public int Used { get; set; }

        public int Unmapped { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .MemberSelection(MemberSelection.Explicit)
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
                .Members(source =>
                {
                    _ = source.Legacy;
                    return new() { Used = source.Used };
                });
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var contract = (ITypeMapper<Source, Destination>)mapper;
            var source = new Source { Used = 17, Unused = 23 };
            var created = contract.Create(
                source,
                default(MappingContext));

            if (created is null ||
                created.Used != 17 ||
                created.Unmapped != 0 ||
                source.GetLegacyReads() != 0)
            {
                throw new InvalidOperationException(
                    "Completeness warnings changed Create behavior.");
            }

            var existing = new Destination
            {
                Used = -1,
                Unmapped = 99
            };
            var updated = contract.Update(
                source,
                existing,
                default(MappingContext));

            if (!ReferenceEquals(updated, existing) ||
                existing.Used != 17 ||
                existing.Unmapped != 99 ||
                source.GetLegacyReads() != 0)
            {
                throw new InvalidOperationException(
                    "Completeness warnings changed Update behavior.");
            }
        }
    }
}
