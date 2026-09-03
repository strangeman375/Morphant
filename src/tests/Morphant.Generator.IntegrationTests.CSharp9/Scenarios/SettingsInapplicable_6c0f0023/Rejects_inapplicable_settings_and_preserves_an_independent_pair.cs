// Compiled integration scenario: SettingsDiagnosticsTests::Rejects_inapplicable_settings_and_preserves_an_independent_pair
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0023

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SettingsInapplicable_6c0f0023
{
    public sealed class ManualSource { }
    public sealed class ManualDestination { }
    public sealed class OpaqueSource { }

    public sealed class ValidSource
    {
        public int Value { get; set; }
    }

    public sealed class ValidDestination
    {
        public int Value { get; set; }
    }

    public static class CallbackTracker
    {
        public static int Manual;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ManualSource, ManualDestination>(MappingMode.Create)
                .NullSourceHandling(NullSourceHandling.Default)
                .NullDestinationHandling(NullDestinationHandling.Throw)
                .ConstructorSelection(ConstructorSelection.Default)
                .MemberSelection(MemberSelection.Auto)
                .UnmappedMemberValidation(UnmappedMemberValidation.Strict)
                .Convert(source =>
                {
                    CallbackTracker.Manual++;
                    return new ManualDestination();
                });

            builder.Map<OpaqueSource, int>(MappingMode.Update)
                .ConstructorSelection(ConstructorSelection.Default);

            builder.Map<ValidSource, ValidDestination>();
        }
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);
            var manual =
                (ITypeMapper<ManualSource, ManualDestination>)mapper;

            ExpectConfigurationFailure(() =>
                manual.Create(new ManualSource(), context));
            ExpectConfigurationFailure(() => manual.Update(
                new ManualSource(),
                new ManualDestination(),
                context));

            var opaque = (ITypeMapper<OpaqueSource, int>)mapper;
            ExpectConfigurationFailure(() =>
                opaque.Create(new OpaqueSource(), context));
            ExpectConfigurationFailure(() =>
                opaque.Update(new OpaqueSource(), 17, context));

            var valid =
                (ITypeMapper<ValidSource, ValidDestination>)mapper;
            var created = valid.Create(
                new ValidSource { Value = 23 },
                context);

            if (created.Value != 23 || CallbackTracker.Manual != 0)
            {
                throw new InvalidOperationException(
                    "Inapplicable setting recovery affected another pair or " +
                    "executed the rejected callback.");
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
                "An inapplicable setting did not reject both operations.");
        }
    }
}
