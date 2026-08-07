using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.IntegrationTests.TestUtils;

namespace Morphant.Generator.IntegrationTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class ConfigurationChainTests
{
    [Test]
    public void Does_not_add_base_pair_registrations_to_the_derived_mapper()
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
    public sealed class BaseSource
    {
        public int Value { get; init; }
    }

    public sealed class BaseDestination
    {
        public int Value { get; set; }
    }

    public sealed class LocalSource
    {
        public int Value { get; init; }
    }

    public sealed class LocalDestination
    {
        public int Value { get; set; }
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.NullSourceHandling(NullSourceHandling.Throw);
            builder.Map<BaseSource, BaseDestination>();
        }
    }

    [MorphantMapper]
    public partial class ConnectedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<LocalSource, LocalDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var baseContract =
                typeof(ITypeMapper<BaseSource, BaseDestination>);
            var localContract =
                typeof(ITypeMapper<LocalSource, LocalDestination>);

            if (baseContract.IsAssignableFrom(typeof(ConnectedMapper)) ||
                !localContract.IsAssignableFrom(typeof(ConnectedMapper)))
            {
                throw new InvalidOperationException(
                    "base.Configure changed the derived mapper registrations.");
            }

            var mapper =
                (ITypeMapper<LocalSource, LocalDestination>)
                new ConnectedMapper();
            var result = mapper.Create(
                new LocalSource { Value = 17 },
                default);

            if (result.Value != 17)
            {
                throw new InvalidOperationException(
                    "The local mapping was not generated.");
            }

            try
            {
                mapper.Create(null, default);
            }
            catch (ArgumentNullException)
            {
                return;
            }

            throw new InvalidOperationException(
                "The connected base root setting was not inherited.");
        }
    }
}
""";

        ProductionGeneratorIntegrationTest.RunAndExecute(
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

        ProductionGeneratorIntegrationTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
