// Compiled integration scenario: SettingsDiagnosticsTests::Applies_each_invalid_value_recovery_family
#nullable enable
#pragma warning disable CS1591
#pragma warning disable MORPH0021

using System;
using Morphant;
using Morphant.Context;
using Morphant.Exceptions;

namespace Morphant.Generator.IntegrationTests.CSharp9.Scenarios.SettingsRecovery_6c0f0021
{
    public sealed class ModeSource { }
    public sealed class ModeDestination { }
    public sealed class NullSourceSource { }
    public sealed class NullSourceDestination { }
    public sealed class NullDestinationSource { }
    public sealed class NullDestinationDestination { }
    public sealed class MemberSource { }
    public sealed class MemberDestination { }

    public sealed class ConstructorSource
    {
        public int Value { get; set; }
    }

    public sealed class ConstructorDestination
    {
        public int Value { get; set; }
    }

    public sealed class ValidationSource
    {
        public int Value { get; set; }
    }

    public sealed class ValidationDestination
    {
        public int Value { get; set; }
    }

    public static class CallbackTracker
    {
        public static int Mode;
        public static int NullSource;
        public static int NullDestination;
        public static int Member;
    }

    [MorphantMapper]
    public partial class TestMapper : TypeMapper<TestMapper>
    {
        protected override void Configure(MapperBuilder builder)
        {
            builder.Map<ModeSource, ModeDestination>(GetMappingMode())
                .Convert(source =>
                {
                    CallbackTracker.Mode++;
                    return new ModeDestination();
                });

            builder.Map<NullSourceSource, NullSourceDestination>(
                    MappingMode.Create)
                .NullSourceHandling(GetNullSourceHandling())
                .ConstructUsing(source =>
                {
                    CallbackTracker.NullSource++;
                    return new NullSourceDestination();
                });

            builder.Map<NullDestinationSource, NullDestinationDestination>()
                .NullDestinationHandling(GetNullDestinationHandling())
                .ConstructUsing(source =>
                {
                    CallbackTracker.NullDestination++;
                    return new NullDestinationDestination();
                });

            builder.Map<MemberSource, MemberDestination>(MappingMode.Create)
                .MemberSelection(GetMemberSelection())
                .ConstructUsing(source =>
                {
                    CallbackTracker.Member++;
                    return new MemberDestination();
                });

            builder.Map<ConstructorSource, ConstructorDestination>(
                    MappingMode.Update)
                .NullDestinationHandling(NullDestinationHandling.Create)
                .ConstructorSelection(GetConstructorSelection());

            builder.Map<ValidationSource, ValidationDestination>()
                .UnmappedMemberValidation(GetUnmappedMemberValidation());
        }

        private static MappingMode GetMappingMode() =>
            MappingMode.Create;

        private static NullSourceHandling GetNullSourceHandling() =>
            NullSourceHandling.Throw;

        private static NullDestinationHandling
            GetNullDestinationHandling() =>
            NullDestinationHandling.Throw;

        private static MemberSelection GetMemberSelection() =>
            MemberSelection.Auto;

        private static ConstructorSelection GetConstructorSelection() =>
            ConstructorSelection.Unambiguous;

        private static UnmappedMemberValidation
            GetUnmappedMemberValidation() =>
            UnmappedMemberValidation.Strict;
    }

    public static class Scenario
    {
        public static void Verify()
        {
            var mapper = new TestMapper();
            var context = default(MappingContext);

            var mode = (ITypeMapper<ModeSource, ModeDestination>)mapper;
            ExpectConfigurationFailure(() =>
                mode.Create(new ModeSource(), context));
            ExpectConfigurationFailure(() =>
                mode.Update(
                    new ModeSource(),
                    new ModeDestination(),
                    context));

            var nullSource =
                (ITypeMapper<NullSourceSource, NullSourceDestination>)mapper;
            ExpectConfigurationFailure(() =>
                nullSource.Create(new NullSourceSource(), context));
            ExpectUnsupported(() => nullSource.Update(
                new NullSourceSource(),
                new NullSourceDestination(),
                context));

            var nullDestination =
                (ITypeMapper<NullDestinationSource,
                    NullDestinationDestination>)mapper;
            _ = nullDestination.Create(
                new NullDestinationSource(),
                context);
            ExpectConfigurationFailure(() => nullDestination.Update(
                new NullDestinationSource(),
                new NullDestinationDestination(),
                context));

            var member =
                (ITypeMapper<MemberSource, MemberDestination>)mapper;
            ExpectConfigurationFailure(() =>
                member.Create(new MemberSource(), context));
            ExpectUnsupported(() => member.Update(
                new MemberSource(),
                new MemberDestination(),
                context));

            var constructor =
                (ITypeMapper<ConstructorSource,
                    ConstructorDestination>)mapper;
            ExpectUnsupported(() => constructor.Create(
                new ConstructorSource(),
                context));
            var existing = new ConstructorDestination();
            var updated = constructor.Update(
                new ConstructorSource { Value = 37 },
                existing,
                context);

            if (!ReferenceEquals(existing, updated) || updated.Value != 37)
            {
                throw new InvalidOperationException(
                    "Invalid ConstructorSelection blocked existing Update.");
            }

            ExpectConfigurationFailure(() => constructor.Update(
                new ConstructorSource { Value = 41 },
                null,
                context));

            var validation =
                (ITypeMapper<ValidationSource, ValidationDestination>)mapper;
            var created = validation.Create(
                new ValidationSource { Value = 43 },
                context);
            var validationExisting = new ValidationDestination();
            var validationUpdated = validation.Update(
                new ValidationSource { Value = 47 },
                validationExisting,
                context);

            if (created.Value != 43 ||
                !ReferenceEquals(validationExisting, validationUpdated) ||
                validationUpdated.Value != 47)
            {
                throw new InvalidOperationException(
                    "Invalid UnmappedMemberValidation changed runtime mapping.");
            }

            if (CallbackTracker.Mode != 0 ||
                CallbackTracker.NullSource != 0 ||
                CallbackTracker.NullDestination != 1 ||
                CallbackTracker.Member != 0)
            {
                throw new InvalidOperationException(
                    "An invalid setting executed an unavailable callback.");
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
                "An invalid setting path did not throw its recovery failure.");
        }

        private static void ExpectUnsupported(Action action)
        {
            try
            {
                action();
            }
            catch (MappingOperationNotSupportedException)
            {
                return;
            }

            throw new InvalidOperationException(
                "A disabled operation did not remain unsupported.");
        }
    }
}
