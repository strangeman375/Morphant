// Compiled integration scenario: InheritanceDiagnosticsTests::Duplicate_base_configuration_rejects_every_known_pair
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0024

using Morphant;
using Morphant.Context;
using Morphant.Exceptions;
using System;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.InheritanceMapperRecovery_7c0f0024
{
    public sealed class CreateSource
    {
    }

    public sealed class CreateDestination
    {
    }

    public sealed class UpdateSource
    {
    }

    public sealed class UpdateDestination
    {
    }

    public sealed class ValidSource
    {
        public int Value { get; init; }
    }

    public sealed class ValidDestination
    {
        public int Value { get; set; }
    }

    public abstract class BaseMapper<TMapper> : TypeMapper<TMapper>
        where TMapper : BaseMapper<TMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
        }
    }

    [MorphantMapper]
    public partial class InvalidMapper : BaseMapper<InvalidMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            base.Configure(builder);
            base.Configure(builder);
            builder.Map<CreateSource, CreateDestination>(MappingMode.Create);
            builder.Map<UpdateSource, UpdateDestination>(MappingMode.Update);
        }
    }

    [MorphantMapper]
    public partial class ValidMapper : TypeMapper<ValidMapper>
    {
        protected override void Configure(MapperBuilder builder) =>
            builder.Map<ValidSource, ValidDestination>();
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var invalid = new InvalidMapper();

            ExpectConfigurationFailure(() =>
                ((ITypeMapper<CreateSource, CreateDestination>)invalid)
                    .Create(new CreateSource(), default));
            ExpectConfigurationFailure(() =>
                ((ITypeMapper<CreateSource, CreateDestination>)invalid)
                    .Update(
                        new CreateSource(),
                        new CreateDestination(),
                        default));
            ExpectConfigurationFailure(() =>
                ((ITypeMapper<UpdateSource, UpdateDestination>)invalid)
                    .Create(new UpdateSource(), default));
            ExpectConfigurationFailure(() =>
                ((ITypeMapper<UpdateSource, UpdateDestination>)invalid)
                    .Update(
                        new UpdateSource(),
                        new UpdateDestination(),
                        default));

            var valid =
                ((ITypeMapper<ValidSource, ValidDestination>)new ValidMapper())
                    .Create(
                        new ValidSource { Value = 42 },
                        new MappingContext());

            if (valid.Value != 42)
            {
                throw new InvalidOperationException(
                    "An independent mapper did not execute.");
            }
        }

        private static void ExpectConfigurationFailure(Action action)
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
                "An invalid inheritance chain was executed.");
        }
    }
}
