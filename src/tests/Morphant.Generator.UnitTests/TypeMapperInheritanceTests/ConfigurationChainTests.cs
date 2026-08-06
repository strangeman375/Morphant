using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class ConfigurationChainTests
{
    [Test]
    public void Connects_an_unannotated_base_mapper_only_through_base_Configure()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
{
    public sealed class Source
    {
        public int Value { get; init; }
    }

    public sealed class Destination
    {
        public int Value { get; set; }
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    [MorphantMapper]
    public partial class ConnectedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            base.Configure(builder);
    }

    [MorphantMapper]
    public partial class DisconnectedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var contract = typeof(ITypeMapper<Source, Destination>);

            var connected =
                contract.IsAssignableFrom(typeof(ConnectedMapper));
            var disconnected =
                contract.IsAssignableFrom(typeof(DisconnectedMapper));

            if (!connected || disconnected)
            {
                throw new InvalidOperationException(
                    "The explicit base Configure boundary was not preserved: " +
                    connected + "/" + disconnected + ".");
            }

            var mapper =
                (ITypeMapper<Source, Destination>)new ConnectedMapper();
            var result = mapper.Create(
                new Source { Value = 17 },
                default);

            if (result.Value != 17)
            {
                throw new InvalidOperationException(
                    "The inherited-only mapping was not generated.");
            }
        }
    }
}
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Repeated_pair_starts_clean_but_keeps_connected_root_settings()
    {
        // lang=c#
        const string source =
"""
#nullable enable
#pragma warning disable CS1591

using Morphant;
using System;

namespace TestCase
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

    public abstract class BaseMapper : TypeMapper
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
""";

        BasicMembersTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
