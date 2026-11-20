// Compiled integration scenario: IncludeMembers diagnostics recovery
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0049
#pragma warning disable MORPH0050

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.IncludeMembersDiagnosticsRecovery_7e2b0902
{
    public sealed class InvalidSource
    {
        public Details Details { get; init; } = new Details();
    }

    public sealed class Details
    {
        public string Name { get; init; } = string.Empty;
    }

    public sealed class InvalidDestination
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class AmbiguousSource
    {
        public Details Left { get; init; } = new Details();

        public Details Right { get; init; } = new Details();
    }

    public sealed class AmbiguousDestination
    {
        public string Name { get; set; } = string.Empty;
    }

    public sealed class ValidSource
    {
        public int Value { get; init; }
    }

    public sealed class ValidDestination
    {
        public int Value { get; set; }
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<InvalidSource, InvalidDestination>()
                .IncludeMembers(source => Select(source));
            builder.Map<AmbiguousSource, AmbiguousDestination>()
                .IncludeMembers(source => source.Left)
                .IncludeMembers(source => source.Right);
            builder.Map<ValidSource, ValidDestination>();
        }

        private static Details Select(InvalidSource source) =>
            source.Details;
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var validMapper =
                (ITypeMapper<ValidSource, ValidDestination>)mapper;
            var valid = validMapper.Create(
                new ValidSource { Value = 31 },
                default(MappingContext));

            if (valid.Value != 31)
            {
                throw new InvalidOperationException(
                    "An invalid IncludeMembers pair affected an independent " +
                    "mapping.");
            }

            AssertConfigurationFailure(() =>
                ((ITypeMapper<InvalidSource, InvalidDestination>)mapper)
                .Create(new InvalidSource(), default(MappingContext)));
            AssertConfigurationFailure(() =>
                ((ITypeMapper<AmbiguousSource, AmbiguousDestination>)mapper)
                .Create(new AmbiguousSource(), default(MappingContext)));
        }

        private static void AssertConfigurationFailure(Action action)
        {
            try
            {
                action();
            }
            catch (MappingConfigurationException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A suppressed IncludeMembers diagnostic became executable.");
        }
    }
}
