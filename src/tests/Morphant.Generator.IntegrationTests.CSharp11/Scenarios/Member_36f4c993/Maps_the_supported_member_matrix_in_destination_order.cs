// Compiled integration scenario: TypeMapperConventionTests/MemberTests::Maps_the_supported_member_matrix_in_destination_order
#nullable enable
#pragma warning disable CS1591

using Morphant;
using Morphant.Context;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp11.Scenarios.Member_36f4c993
{
    public class SourceBase
    {
        public int Inherited { get; init; }

        public int Hidden { get; init; }
    }

    public sealed class Source : SourceBase
    {
        public int Settable { get; init; }

        public int InitOnly { get; init; }

        public int RequiredSet { get; init; }

        public int RequiredInit { get; init; }

        public int RequiredField { get; init; }

        public int MutableField;

        public readonly int ReadonlySource = 11;

        public int ReadonlyField { get; init; }

        public int GetOnly { get; init; }

        public int PrivateSet { get; init; }

        public int SetOnly
        {
            set { }
        }

        public int PrivateGet { private get; set; }

        public int this[int index] => index;
    }

    public class DestinationBase
    {
        public int Inherited { get; set; }

        public int Hidden { get; set; }
    }

    public sealed class Destination : DestinationBase
    {
        public new int Hidden { get; } = 31;

        public int Settable { get; set; }

        public int InitOnly { get; init; }

        public required int RequiredSet { get; set; }

        public required int RequiredInit { get; init; }

        public required int RequiredField;

        public int MutableField;

        public int ReadonlySource { get; set; }

        public readonly int ReadonlyField = 37;

        public int GetOnly { get; } = 41;

        public int PrivateSet { get; private set; } = 43;

        public int SetOnly { get; set; } = 47;

        public int PrivateGet { get; set; } = 49;

        public static int Static { get; set; }

        public int this[int index]
        {
            get => index;
            set { }
        }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
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
                Inherited = 1,
                Hidden = 2,
                Settable = 3,
                InitOnly = 4,
                RequiredSet = 5,
                RequiredInit = 6,
                RequiredField = 7,
                MutableField = 8,
                ReadonlyField = 9,
                GetOnly = 10,
                PrivateSet = 12
            };
            var created = mapper.Create(source, default(MappingContext));
            var previous = new Destination
            {
                Inherited = 11,
                Settable = 12,
                InitOnly = 13,
                RequiredSet = 14,
                RequiredInit = 15,
                RequiredField = 16,
                MutableField = 17
            };
            var updated = mapper.Update(
                source,
                previous,
                default(MappingContext));

            if (created.Inherited != 1 ||
                created.Hidden != 31 ||
                created.Settable != 3 ||
                created.InitOnly != 4 ||
                created.RequiredSet != 5 ||
                created.RequiredInit != 6 ||
                created.RequiredField != 7 ||
                created.MutableField != 8 ||
                created.ReadonlySource != 11 ||
                created.ReadonlyField != 37 ||
                created.GetOnly != 41 ||
                created.PrivateSet != 43 ||
                created.SetOnly != 47 ||
                created.PrivateGet != 49 ||
                !ReferenceEquals(updated, previous) ||
                updated.Inherited != 1 ||
                updated.Settable != 3 ||
                updated.InitOnly != 13 ||
                updated.RequiredSet != 5 ||
                updated.RequiredInit != 15 ||
                updated.RequiredField != 7 ||
                updated.MutableField != 8 ||
                updated.ReadonlySource != 11 ||
                updated.SetOnly != 47 ||
                updated.PrivateGet != 49)
            {
                throw new InvalidOperationException(
                    "The convention member matrix was not preserved.");
            }
        }
    }
}
