using Microsoft.CodeAnalysis.CSharp;
using Morphant.Generator.UnitTests.TestUtils;

namespace Morphant.Generator.UnitTests.TypeMapperInheritanceTests;

[TestFixture]
internal sealed class SettingsCompositionTests
{
    [Test]
    public void Resolves_pair_then_current_and_connected_root_settings_with_Default()
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
        public int Id { get; init; }

        public int Value { get; init; }
    }

    public sealed class IncludedDestination
    {
        public IncludedDestination()
        {
            Kind = "parameterless";
        }

        public IncludedDestination(
            int id,
            string label = "largest")
        {
            Kind = label + ":" + id;
        }

        public string Kind { get; }

        public int Value { get; set; }
    }

    public sealed class RootDestination
    {
        public int Value { get; set; }
    }

    public abstract class FarMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.MappingMode(MappingMode.Create);
            builder.NullSourceHandling(NullSourceHandling.Throw);
            builder.NullDestinationHandling(NullDestinationHandling.Create);
            builder.ConstructorSelection(ConstructorSelection.Largest);
            builder.MemberSelection(MemberSelection.Explicit);
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Strict);

            builder.Map<Source, IncludedDestination>(
                    MappingMode.CreateAndUpdate)
                .NullSourceHandling(NullSourceHandling.ReturnNull)
                .NullDestinationHandling(
                    NullDestinationHandling.Create)
                .ConstructorSelection(ConstructorSelection.Largest)
                .MemberSelection(MemberSelection.Auto)
                .UnmappedMemberValidation(
                    UnmappedMemberValidation.Destination);
        }
    }

    public abstract class NearMapper : FarMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);

            builder.MappingMode(MappingMode.Update);
            builder.NullSourceHandling(
                NullSourceHandling.ReturnDestination);
            builder.NullDestinationHandling(
                NullDestinationHandling.Throw);
            builder.ConstructorSelection(
                ConstructorSelection.Parameterless);
            builder.MemberSelection(MemberSelection.Auto);
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Source);

            builder.Map<Source, IncludedDestination>()
                .IncludeBase()
                .NullSourceHandling(NullSourceHandling.Throw)
                .NullSourceHandling(NullSourceHandling.Default)
                .ConstructorSelection(ConstructorSelection.Explicit)
                .ConstructorSelection(ConstructorSelection.Default);
        }
    }

    [MorphantMapper]
    public partial class DerivedMapper : NearMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);

            builder.MappingMode(MappingMode.Create);
            builder.MappingMode(MappingMode.Default);
            builder.NullSourceHandling(NullSourceHandling.Throw);
            builder.NullSourceHandling(NullSourceHandling.Default);
            builder.NullDestinationHandling(
                NullDestinationHandling.Create);
            builder.NullDestinationHandling(
                NullDestinationHandling.Default);
            builder.ConstructorSelection(
                ConstructorSelection.Explicit);
            builder.ConstructorSelection(ConstructorSelection.Default);
            builder.MemberSelection(MemberSelection.Explicit);
            builder.MemberSelection(MemberSelection.Default);
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.None);
            builder.UnmappedMemberValidation(
                UnmappedMemberValidation.Default);

            builder.Map<Source, IncludedDestination>()
                .IncludeBase();
            builder.Map<Source, RootDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new DerivedMapper();
            var included =
                (ITypeMapper<Source, IncludedDestination>)mapper;
            var source = new Source { Id = 17, Value = 31 };
            var created = included.Create(source, default);
            var recreated = included.Update(source, null, default);

            if (created.Kind != "largest:17" ||
                created.Value != 31 ||
                recreated.Kind != "largest:17" ||
                included.Create(null, default) is not null)
            {
                throw new InvalidOperationException(
                    "Included pair settings did not outrank connected roots.");
            }

            var root = (ITypeMapper<Source, RootDestination>)mapper;
            var previous = new RootDestination { Value = 7 };

            if (!ReferenceEquals(
                    previous,
                    root.Update(null, previous, default)))
            {
                throw new InvalidOperationException(
                    "Default did not continue to the nearest base root.");
            }

            ExpectNotSupported(() => root.Create(source, default));
            ExpectArgumentNull(() => root.Update(source, null, default));
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
                "Connected root MappingMode was not applied.");
        }

        private static void ExpectArgumentNull(Action action)
        {
            try
            {
                action();
            }
            catch (ArgumentNullException)
            {
                return;
            }

            throw new InvalidOperationException(
                "Connected root NullDestinationHandling was not applied.");
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
