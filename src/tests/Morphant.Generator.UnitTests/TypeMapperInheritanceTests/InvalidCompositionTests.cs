using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class InvalidCompositionTests
{
    [Test]
    public void Preserves_invalid_IncludeBase_forms_as_unsupported_states()
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

    public sealed class MissingDestination
    {
    }

    public sealed class ExistingDestination
    {
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, ExistingDestination>();
    }

    [MorphantMapper]
    public partial class NoChainMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, MissingDestination>().IncludeBase();
    }

    [MorphantMapper]
    public partial class MissingPairMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, MissingDestination>().IncludeBase();
        }
    }

    [MorphantMapper]
    public partial class DuplicateIncludeMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, ExistingDestination>()
                .IncludeBase()
                .IncludeBase();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var source = new Source { Value = 17 };

            ExpectNotSupported(() =>
                ((ITypeMapper<Source, MissingDestination>)
                    new NoChainMapper()).Create(source, default));
            ExpectNotSupported(() =>
                ((ITypeMapper<Source, MissingDestination>)
                    new MissingPairMapper()).Create(source, default));
            ExpectNotSupported(() =>
                ((ITypeMapper<Source, ExistingDestination>)
                    new DuplicateIncludeMapper()).Create(source, default));
        }

        private static void ExpectNotSupported(Action action)
        {
            try
            {
                action();
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "An invalid IncludeBase form was silently accepted.");
        }
    }
}
""";

        ConventionTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Rejects_duplicate_base_Configure_calls_for_the_mapper()
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
    }

    public sealed class Destination
    {
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>();
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            base.Configure(builder);
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            try
            {
                ((ITypeMapper<Source, Destination>)new DerivedMapper())
                    .Create(new Source(), default);
            }
            catch (NotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Duplicate base Configure calls were accepted.");
        }
    }
}
""";

        ConventionTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }

    [Test]
    public void Ignores_inherited_declarative_settings_after_local_Convert_replacement()
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
        public Destination(int value) => Value = value;

        public int Value { get; }
    }

    public abstract class BaseMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<Source, Destination>()
                .ConstructorSelection(ConstructorSelection.Explicit)
                .MemberSelection(MemberSelection.Explicit)
                .Construct(source => new(source.Value));
    }

    [MorphantMapper]
    public partial class DerivedMapper : BaseMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            builder.Map<Source, Destination>()
                .IncludeBase()
                .Convert((source, _, _) =>
                    new Destination(source?.Value ?? -1));
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var result =
                ((ITypeMapper<Source, Destination>)new DerivedMapper())
                    .Create(new Source { Value = 17 }, default);

            if (result.Value != 17)
            {
                throw new InvalidOperationException(
                    "Inherited no-effect settings invalidated local Convert.");
            }
        }
    }
}
""";

        StructuredConstructTypeMapperGeneratorTest.RunAndExecute(
            LanguageVersion.CSharp9,
            source,
            "TestCase.Scenario");
    }
}
